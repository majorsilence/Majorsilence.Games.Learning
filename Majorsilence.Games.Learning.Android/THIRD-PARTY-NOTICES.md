# Third-party notices

This project vendors prebuilt Android binaries of the SDL libraries under
`android/` (the Java classes jar and the per-ABI `.so` files). They were
extracted unmodified from the official release archives published by the SDL
project (libsdl.org):

| Component | Version | Source archive |
|-----------|---------|----------------|
| SDL3 (`libSDL3.so`, `SDL3-classes.jar`) | 3.4.10 | https://github.com/libsdl-org/SDL/releases/tag/release-3.4.10 |
| SDL3_image (`libSDL3_image.so`) | 3.4.4 | https://github.com/libsdl-org/SDL_image/releases/tag/release-3.4.4 |
| SDL3_ttf (`libSDL3_ttf.so`) | 3.2.2 | https://github.com/libsdl-org/SDL_ttf/releases/tag/release-3.2.2 |
| SDL3_mixer (`libSDL3_mixer.so`) | 3.2.4 | https://github.com/libsdl-org/SDL_mixer/releases/tag/release-3.2.4 |

Versions are kept in lockstep with the SDL3-CS binding packages used by the
desktop project; refresh these binaries from the matching release archives
whenever those bindings are upgraded.

## SDL license (zlib)

SDL3, SDL3_image, SDL3_ttf, and SDL3_mixer are distributed under the zlib
license:

```
This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
```

## Libraries statically linked into the official SDL builds

The official binaries above bundle the following third-party code, all under
permissive licenses. No LGPL or GPL components (e.g. mpg123, FluidSynth) are
linked into these builds.

- **FreeType** (in SDL3_ttf) — FreeType License (FTL).
  Portions of this software are copyright © The FreeType Project
  (https://www.freetype.org). All rights reserved.
- **HarfBuzz** (in SDL3_ttf) — MIT ("Old MIT") license.
- **libpng** (in SDL3_image) — PNG Reference Library License.
- **stb_image / Tiny JPEG Encoder** (in SDL3_image) — public domain / MIT.
- **stb_vorbis, dr_flac, dr_mp3** (in SDL3_mixer) — public domain / MIT-0.
- **Opus / opusfile** (in SDL3_mixer) — BSD.
- **WavPack** (in SDL3_mixer) — BSD.
- **libxmp** (in SDL3_mixer) — MIT.
- **TiMidity** (in SDL3_mixer) — Artistic License.
