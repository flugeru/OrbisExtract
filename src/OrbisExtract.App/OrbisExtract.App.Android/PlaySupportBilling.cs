using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Android.BillingClient.Api;
using Avalonia.Threading;

namespace OrbisExtract.App.Android;

internal sealed class PlaySupportBilling : Java.Lang.Object, IPurchasesUpdatedListener
{
    internal const string ProductId = "orbisextract_apoio";
    internal const string PurchaseOptionId = "apoio-unico";

    internal enum SupportState { Loading, Available, Unavailable, Pending, Supporter, Error, Checkout }
    internal sealed record Snapshot(SupportState State, string? Price = null);

    private readonly MainActivity activity;
    private readonly BillingClient client;
    private readonly byte[] publicKey;
    private bool stopped, busy, checkout, refreshQueued;
    private int purchaseUpdates;

    internal Snapshot Status { get; private set; } = new(SupportState.Unavailable);
    internal event Action? Changed;

    private PlaySupportBilling(MainActivity activity, byte[] publicKey)
    {
        this.activity = activity;
        this.publicKey = publicKey;
        client = BillingClient.NewBuilder(activity)
            .SetListener(this)
            .EnablePendingPurchases(PendingPurchasesParams.NewBuilder().EnableOneTimeProducts().Build())
            .EnableAutoServiceReconnection()
            .Build();
    }

    internal static PlaySupportBilling? Create(MainActivity activity)
    {
        var value = typeof(PlaySupportBilling).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "PlayBillingPublicKey")?.Value;
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var bytes = Convert.FromBase64String(value);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(bytes, out var consumed);
            return consumed == bytes.Length ? new(activity, bytes) : null;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    internal Task RefreshAsync() => RunAsync(purchase: false);
    internal Task BuyAsync() => RunAsync(purchase: true);

    private async Task RunAsync(bool purchase)
    {
        if (stopped || checkout) return;
        if (busy || purchaseUpdates != 0)
        {
            if (!purchase) refreshQueued = true;
            return;
        }
        busy = true;
        var displayedPrice = Status.Price;
        Set(SupportState.Loading);
        try
        {
            if (!client.IsReady && (await client.StartConnectionAsync()).ResponseCode != BillingResponseCode.Ok)
            {
                Set(SupportState.Unavailable);
                return;
            }
            if (stopped) return;
            var owned = await client.QueryPurchasesAsync(QueryPurchasesParams.NewBuilder()
                .SetProductType(BillingClient.ProductType.Inapp).Build());
            if (stopped) return;
            if (owned.Result.ResponseCode != BillingResponseCode.Ok)
            {
                Set(SupportState.Unavailable);
                return;
            }
            var status = await GetOwnershipAsync(owned.Purchases);
            if (stopped) return;
            if (status is { } state)
            {
                Set(state);
                return;
            }

            var result = await client.QueryProductDetailsAsync(QueryProductDetailsParams.NewBuilder()
                .SetProductList([QueryProductDetailsParams.Product.NewBuilder().SetProductId(ProductId)
                    .SetProductType(BillingClient.ProductType.Inapp).Build()]).Build());
            if (stopped) return;
            var product = result.Result.ResponseCode == BillingResponseCode.Ok
                ? result.ProductDetails.FirstOrDefault(item => item.ProductId == ProductId) : null;
            var offer = product?.OneTimePurchaseOfferDetailsList?.FirstOrDefault(item =>
                item.PurchaseOptionId == PurchaseOptionId && string.IsNullOrEmpty(item.OfferId) &&
                item.GetRentalDetails() is null && item.GetPreorderDetails() is null);
            if (product is null || offer?.OfferToken is not { Length: > 0 } token || string.IsNullOrEmpty(offer.FormattedPrice))
            {
                Set(SupportState.Unavailable);
                return;
            }
            // Refresh the quote before checkout. If it changed, let the user see it and tap again.
            if (!purchase || displayedPrice != offer.FormattedPrice || activity.IsFinishing || activity.IsDestroyed)
            {
                Set(SupportState.Available, offer.FormattedPrice);
                return;
            }
            var parameters = BillingFlowParams.NewBuilder().SetProductDetailsParamsList([
                BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(product).SetOfferToken(token).Build()
            ]).Build();
            checkout = true;
            Set(SupportState.Checkout);
            var launch = client.LaunchBillingFlow(activity, parameters);
            if (launch.ResponseCode == BillingResponseCode.Ok) return;
            checkout = false;
            if (launch.ResponseCode == BillingResponseCode.ItemAlreadyOwned) refreshQueued = true;
            Set(SupportState.Error);
        }
        catch (Exception)
        {
            checkout = false;
            Set(SupportState.Error);
        }
        finally
        {
            busy = false;
            RefreshIfQueued();
        }
    }

    public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
        // Re-query Play ownership after handling any fresh transaction from checkout.
        var code = result.ResponseCode;
        var updated = purchases?.ToArray();
        Dispatcher.UIThread.Post(() => _ = ProcessUpdateAsync(code, updated));
    }

    private async Task ProcessUpdateAsync(BillingResponseCode code, Purchase[]? purchases)
    {
        if (stopped) return;
        checkout = false;
        purchaseUpdates++;
        try
        {
            if (code == BillingResponseCode.Ok && purchases is not null)
                Set(await GetOwnershipAsync(purchases) ?? SupportState.Unavailable);
            else if (code is not (BillingResponseCode.UserCancelled or BillingResponseCode.ItemAlreadyOwned))
            {
                Set(SupportState.Error);
                return;
            }
            refreshQueued = true;
        }
        catch (Exception) { Set(SupportState.Error); }
        finally
        {
            purchaseUpdates--;
            RefreshIfQueued();
        }
    }

    private async Task<SupportState?> GetOwnershipAsync(IEnumerable<Purchase> purchases)
    {
        bool pending = false;
        foreach (var purchase in purchases)
        {
            if (stopped) return null;
            if (purchase.PackageName != activity.PackageName || !purchase.Products.Contains(ProductId)) continue;
            if (!ValidSignature(purchase)) return SupportState.Error;
            if (purchase.PurchaseState == PurchaseState.Pending)
            {
                pending = true;
                continue;
            }
            if (purchase.PurchaseState != PurchaseState.Purchased) continue;
            if (!purchase.IsAcknowledged)
            {
                var result = await client.AcknowledgePurchaseAsync(AcknowledgePurchaseParams.NewBuilder()
                    .SetPurchaseToken(purchase.PurchaseToken).Build());
                if (result.ResponseCode != BillingResponseCode.Ok) return SupportState.Error;
            }
            return SupportState.Supporter;
        }
        return pending ? SupportState.Pending : null;
    }

    private bool ValidSignature(Purchase purchase)
    {
        try
        {
            // Play signs order JSON using SHA1withRSA. This is only the local cosmetic entitlement.
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return rsa.VerifyData(Encoding.UTF8.GetBytes(purchase.OriginalJson), Convert.FromBase64String(purchase.Signature),
                HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException) { return false; }
    }

    private void RefreshIfQueued()
    {
        if (!refreshQueued || busy || checkout || stopped || purchaseUpdates != 0) return;
        refreshQueued = false;
        _ = RefreshAsync();
    }

    private void Set(SupportState state, string? price = null)
    {
        if (stopped) return;
        Status = new(state, price);
        Changed?.Invoke();
    }

    internal void Stop()
    {
        stopped = true;
        client.EndConnection();
        client.Dispose();
    }
}
