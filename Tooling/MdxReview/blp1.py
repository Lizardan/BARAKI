"""Warcraft 3 BLP1 decoder.

JPEG mipmaps are 4-component (PIL reports CMYK). WC3 stores them as inverted BGRA:
the alpha channel is the team-color / cutout mask, not a CMYK key plate.
"""
from __future__ import annotations

import struct
from io import BytesIO

from PIL import Image, ImageOps


def _u32(buf: bytes, i: int) -> int:
    return struct.unpack_from("<I", buf, i)[0]


def decode_blp(data: bytes) -> Image.Image:
    if data[:4] != b"BLP1":
        raise ValueError("Not BLP1: " + data[:4].decode("latin1", "replace"))
    compression = _u32(data, 4)
    alpha_bits = _u32(data, 8)
    width = _u32(data, 12)
    height = _u32(data, 16)
    mip_off = _u32(data, 28)
    mip_sz = _u32(data, 92)
    if compression == 0:
        return _decode_jpeg(data, mip_off, mip_sz)
    return _decode_paletted(data, width, height, alpha_bits, mip_off, mip_sz)


def _decode_jpeg(data: bytes, mip_off: int, mip_sz: int) -> Image.Image:
    header_size = _u32(data, 156)
    jpeg_header = data[160 : 160 + header_size]
    mip = data[mip_off : mip_off + mip_sz]
    if jpeg_header.startswith(b"\xff\xd8") and mip.startswith(b"\xff\xd8"):
        header = jpeg_header[:-2] if jpeg_header.endswith(b"\xff\xd9") else jpeg_header
        jpeg = header + mip[2:]
    else:
        jpeg = jpeg_header + mip
    img = Image.open(BytesIO(jpeg))
    if img.mode != "CMYK":
        return img.convert("RGBA")
    # PIL exposes the 4 JPEG components as C,M,Y,K = B,G,R,A (inverted).
    cyan, magenta, yellow, key = img.split()
    red = ImageOps.invert(yellow)
    green = ImageOps.invert(magenta)
    blue = ImageOps.invert(cyan)
    alpha = ImageOps.invert(key)
    return Image.merge("RGBA", (red, green, blue, alpha))


def _decode_paletted(
    data: bytes,
    width: int,
    height: int,
    alpha_bits: int,
    mip_off: int,
    mip_sz: int,
) -> Image.Image:
    palette = []
    for i in range(256):
        blue, green, red, alpha = data[156 + i * 4 : 160 + i * 4]
        palette.append((red, green, blue, alpha))
    mip = data[mip_off : mip_off + mip_sz]
    count = width * height
    indices = mip[:count]
    plane = None
    if alpha_bits == 8 and len(mip) >= count * 2:
        plane = mip[count : count * 2]
    elif alpha_bits == 1 and len(mip) >= count + (count + 7) // 8:
        bits = mip[count : count + (count + 7) // 8]
        plane = bytes(((bits[i >> 3] >> (i & 7)) & 1) * 255 for i in range(count))
    pixels = []
    for i, idx in enumerate(indices):
        red, green, blue, palette_alpha = palette[idx]
        alpha = plane[i] if plane is not None else palette_alpha
        pixels.append((red, green, blue, alpha))
    img = Image.new("RGBA", (width, height))
    img.putdata(pixels)
    return img
