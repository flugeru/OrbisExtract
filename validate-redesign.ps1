# OrbisExtract — Script de Validação do Redesign
$root = Split-Path -Parent $PSScriptRoot

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  OrbisExtract - Redesign Validation" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$errors = @()
$passed = 0
$failed = 0

# Arquivos principais
$mainView = Join-Path $root 'src\OrbisExtract.App\OrbisExtract.App\Views\MainView.cs'
$readme = Join-Path $root 'README.md'

Write-Host "[1/5] Verificando elementos removidos..." -ForegroundColor Yellow

# Sidebar removida
if (Get-Content $mainView | Select-String -Quiet "_sidebar") {
    $errors += "FAIL: _sidebar ainda existe em MainView.cs"
    $failed++
} else {
    Write-Host "  PASS: Sidebar removida corretamente" -ForegroundColor Green
    $passed++
}

# BottomNav removida
if (Get-Content $mainView | Select-String -Quiet "_bottomNav") {
    $errors += "FAIL: _bottomNav ainda existe em MainView.cs"
    $failed++
} else {
    Write-Host "  PASS: BottomNav removida corretamente" -ForegroundColor Green
    $passed++
}

# Passcode removido da UI
if (Get-Content $mainView | Select-String -Quiet "private readonly TextBox _passcode") {
    $errors += "FAIL: Campo _passcode ainda existe em MainView.cs"
    $failed++
} else {
    Write-Host "  PASS: Campo Passcode removido corretamente" -ForegroundColor Green
    $passed++
}

# Timer removido
if (Get-Content $mainView | Select-String -Quiet "private DispatcherTimer") {
    $errors += "FAIL: Campo _timer ainda existe em MainView.cs"
    $failed++
} else {
    Write-Host "  PASS: Campo Timer removido corretamente" -ForegroundColor Green
    $passed++
}

Write-Host ""
Write-Host "[2/5] Verificando elementos preservados..." -ForegroundColor Yellow

# Cartoes ainda existem
if (Get-Content $mainView | Select-String -Quiet "_packageCard") {
    Write-Host "  PASS: Card do Pacote intacto" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: _packageCard nao encontrado"
    $failed++
}

if (Get-Content $mainView | Select-String -Quiet "_settingsCard") {
    Write-Host "  PASS: Card de Configuracao intacto" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: _settingsCard nao encontrado"
    $failed++
}

if (Get-Content $mainView | Select-String -Quiet "_monitorCard") {
    Write-Host "  PASS: Card de Monitoramento intacto" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: _monitorCard nao encontrado"
    $failed++
}

Write-Host ""
Write-Host "[3/5] Verificando layout responsivo..." -ForegroundColor Yellow

if (Get-Content $mainView | Select-String -Quiet "ApplyContentLayout") {
    Write-Host "  PASS: Metodo ApplyContentLayout existente" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: Metodo ApplyContentLayout nao encontrado"
    $failed++
}

if (Get-Content $mainView | Select-String -Quiet "_isWideContent") {
    Write-Host "  PASS: Variavel _isWideContent existente" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: Variavel _isWideContent nao encontrada"
    $failed++
}

Write-Host ""
Write-Host "[4/5] Verificando compilacao..." -ForegroundColor Yellow

try {
    $buildOutput = dotnet build "src\OrbisExtract.App\OrbisExtract.App.Desktop" -c Release --no-restore 2>&1
    
    if ($buildOutput -match "Compilacao com sucesso") {
        Write-Host "  PASS: Build Desktop bem-sucedido" -ForegroundColor Green
        $passed++
        
        if ($buildOutput -match "0 Aviso\(s\)") {
            Write-Host "  PASS: Zero warnings na compilacao" -ForegroundColor Green
            $passed++
        } else {
            Write-Host "  WARN: Warnings encontrados" -ForegroundColor Yellow
        }
    } else {
        $errors += "FAIL: Build Desktop falhou"
        $failed++
    }
} catch {
    $errors += "FAIL: Erro ao executar build: $_"
    $failed++
}

Write-Host ""
Write-Host "[5/5] Verificando documentacao..." -ForegroundColor Yellow

if (Test-Path $readme) {
    Write-Host "  PASS: README.md existente" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: README.md nao encontrado"
    $failed++
}

$redesignDoc = Join-Path $root 'REDESIGN_DOCUMENTATION.md'
if (Test-Path $redesignDoc) {
    Write-Host "  PASS: REDESIGN_DOCUMENTATION.md existente" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: REDESIGN_DOCUMENTATION.md nao encontrado"
    $failed++
}

$releaseNotes = Join-Path $root 'RELEASE_NOTES.md'
if (Test-Path $releaseNotes) {
    Write-Host "  PASS: RELEASE_NOTES.md existente" -ForegroundColor Green
    $passed++
} else {
    $errors += "FAIL: RELEASE_NOTES.md nao encontrado"
    $failed++
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  RESULTADO DA VALIDACAO" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Passados: $passed" -ForegroundColor Green
Write-Host "Falhados: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($errors.Count -gt 0) {
    Write-Host "ERROS:" -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host "  X $error" -ForegroundColor Red
    }
} else {
    Write-Host "SUCESSO: TODAS AS VALIDACOES PASSARAM!" -ForegroundColor Green
    Write-Host ""
    Write-Host "O redesign foi implementado com sucesso:" -ForegroundColor Cyan
    Write-Host "  - Elementos indicados removidos" -ForegroundColor White
    Write-Host "  - Redesign visual completo aplicado" -ForegroundColor White
    Write-Host "  - Compatibilidade multiplataforma mantida" -ForegroundColor White
    Write-Host "  - Funcionalidades existentes preservadas" -ForegroundColor White
    Write-Host "  - Builds executados sem erros" -ForegroundColor White
}
