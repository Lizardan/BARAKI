"""Extract WC3 BLP textures referenced by Faceless MDX from a local Warcraft III install."""
from __future__ import annotations

import ctypes
import sys
from pathlib import Path

from blp1 import decode_blp

STORMLIB = Path.home() / "AppData/Roaming/Python/Python314/site-packages/pystormlib/ressources/StormLib_x64.dll"
MPQS = [
    Path(r"G:\Games\Warcraft III iCCup\War3Patch.mpq"),
    Path(r"G:\Games\Warcraft III iCCup\War3x.mpq"),
    Path(r"G:\Games\Warcraft III iCCup\War3xlocal.mpq"),
    Path(r"G:\Games\Warcraft III iCCup\war3.mpq"),
]
DST = Path(r"F:\Unity Projects\BARAKI\Assets\Game\Art\Races\Faceless\Wc3")

NEEDED = [
    r"units\Creeps\FacelessOne\FacelessOne.blp",
    r"units\Creeps\CentaurKhan\CentaurKhan.blp",
    r"units\Undead\UndeadAirBarge\UndeadAirBarge.blp",
    r"units\Creeps\Unbroken\FacelessOneUnbroken.blp",
    r"units\Other\DranaiMage\DranaiPurple.blp",
    r"units\Creeps\UndeadDestroyerShip\UndeadDestroyerShip.blp",
    r"units\Orc\HeroTaurenChieftain\HeroTaurenChieftain.blp",
    r"units\Orc\OrcWarlockGuldan\OrcWarlockGuldan.blp",
    r"units\Undead\SkeletonMage\SkeletonMage.blp",
    r"units\Creeps\Revenant\Revenant.blp",
    r"units\NightElf\HeroMoonPriestess\PriestessOfTheMoon.blp",
    r"Buildings\Human\ArcaneSanctum\ArcaneSanctum.blp",
    r"Textures\SacrificialAltarskull1.blp",
    r"textures\Razormane.blp",
    r"Textures\InfernalCannon.blp",
    r"units\Creeps\HarpyWitch\HarpyWitch.blp",
    r"Units\Creeps\ForgottenOne\ForgottenOne.blp",
    r"units\Naga\NagaRoyalGuard\NagaRoyalGuard.blp",
    r"Textures\HeroAvatarFlame.blp",
]


def load_storm():
    dll = ctypes.WinDLL(str(STORMLIB))
    dll.SFileOpenArchive.restype = ctypes.c_bool
    dll.SFileCloseArchive.restype = ctypes.c_bool
    dll.SFileHasFile.restype = ctypes.c_bool
    dll.SFileOpenFileEx.restype = ctypes.c_bool
    dll.SFileGetFileSize.restype = ctypes.c_uint
    dll.SFileReadFile.restype = ctypes.c_bool
    dll.SFileCloseFile.restype = ctypes.c_bool
    return dll


def open_archive(dll, path: Path):
    handle = ctypes.c_void_p()
    # StormLib TCHAR build: try wide then ansi.
    ok = False
    try:
        dll.SFileOpenArchive.argtypes = [
            ctypes.c_wchar_p,
            ctypes.c_uint,
            ctypes.c_uint,
            ctypes.POINTER(ctypes.c_void_p),
        ]
        ok = dll.SFileOpenArchive(str(path), 0, 0x100, ctypes.byref(handle))
    except Exception:
        ok = False
    if not ok:
        dll.SFileOpenArchive.argtypes = [
            ctypes.c_char_p,
            ctypes.c_uint,
            ctypes.c_uint,
            ctypes.POINTER(ctypes.c_void_p),
        ]
        ok = dll.SFileOpenArchive(str(path).encode("utf-8"), 0, 0x100, ctypes.byref(handle))
    if not ok:
        raise OSError(f"SFileOpenArchive failed: {path} err={ctypes.GetLastError()}")
    return handle


def read_file(dll, archive, inner: str) -> bytes | None:
    dll.SFileHasFile.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
    path_b = inner.encode("ascii")
    if not dll.SFileHasFile(archive, path_b):
        # try forward slashes / other case
        return None
    fh = ctypes.c_void_p()
    dll.SFileOpenFileEx.argtypes = [
        ctypes.c_void_p,
        ctypes.c_char_p,
        ctypes.c_uint,
        ctypes.POINTER(ctypes.c_void_p),
    ]
    if not dll.SFileOpenFileEx(archive, path_b, 0, ctypes.byref(fh)):
        return None
    high = ctypes.c_uint()
    dll.SFileGetFileSize.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint)]
    low = dll.SFileGetFileSize(fh, ctypes.byref(high))
    size = (high.value << 32) | low
    buf = ctypes.create_string_buffer(size)
    read = ctypes.c_uint()
    dll.SFileReadFile.argtypes = [
        ctypes.c_void_p,
        ctypes.c_void_p,
        ctypes.c_uint,
        ctypes.POINTER(ctypes.c_uint),
        ctypes.c_void_p,
    ]
    ok = dll.SFileReadFile(fh, buf, size, ctypes.byref(read), None)
    dll.SFileCloseFile.argtypes = [ctypes.c_void_p]
    dll.SFileCloseFile(fh)
    return buf.raw if ok else None


def main() -> int:
    dll = load_storm()
    archives = []
    for mpq in MPQS:
        if not mpq.exists():
            print("missing", mpq)
            continue
        archives.append((mpq.name, open_archive(dll, mpq)))
        print("opened", mpq.name)
    DST.mkdir(parents=True, exist_ok=True)
    for inner in NEEDED:
        data = None
        src_name = None
        for name, handle in archives:
            data = read_file(dll, handle, inner)
            if data:
                src_name = name
                break
        leaf = inner.split("\\")[-1]
        stem = Path(leaf).stem
        if not data:
            print("NOT FOUND", inner)
            continue
        img = decode_blp(data)
        out = DST / f"{stem}.png"
        img.save(out)
        print(f"ok {stem}.png {img.size} {img.mode} from {src_name} alpha={(img.getextrema()[3] if img.mode=='RGBA' else None)}")
    for _, handle in archives:
        dll.SFileCloseArchive.argtypes = [ctypes.c_void_p]
        dll.SFileCloseArchive(handle)
    return 0


if __name__ == "__main__":
    sys.exit(main())
