"""Validate release native ELF segments and architectures without extra packages."""
import struct
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1]) as bundle:
    libs = [n for n in bundle.namelist() if n.endswith('.so')]
    assert libs, 'No native libraries found'
    abis = {n.split('/')[2] for n in libs if n.startswith('base/lib/')}
    assert 'arm64-v8a' in abis, abis
    for name in libs:
        data = bundle.read(name)
        assert data[:4] == b'\x7fELF', name
        assert data[4] == 2 and data[5] == 1, f'Expected little-endian 64-bit ELF: {name}'
        offset = struct.unpack_from('<Q', data, 32)[0]
        size, count = struct.unpack_from('<HH', data, 54)
        for i in range(count):
            pos = offset + i * size
            if struct.unpack_from('<I', data, pos)[0] != 1:
                continue
            file_offset, virtual_address = struct.unpack_from('<QQ', data, pos + 8)
            alignment = struct.unpack_from('<Q', data, pos + 48)[0]
            assert alignment >= 16384, f'4 KB ELF segment: {name}'
            assert (virtual_address - file_offset) % 16384 == 0, f'Misaligned ELF segment: {name}'
    print(f'Validated {len(libs)} native libraries, 16 KB ELF alignment; ABIs: {sorted(abis)}')
