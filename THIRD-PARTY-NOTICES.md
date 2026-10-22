# Third-party notices

Windows Dynamic Island is open source under the [MIT License](LICENSE)
(c) 2026 acheeseburgercodes. The MIT license text is also attached to every
GitHub release as the `LICENSE` asset and shown during installation.

The release bundles third-party open-source components. Each component
remains licensed under its own terms, reproduced below where required.

## Inno Setup (installer)

The Windows installer is built with
[Inno Setup](https://jrsoftware.org/isinfo.php) by Jordan Russell and the
Inno Setup community, distributed under the permissive
[Inno Setup License](https://jrsoftware.org/files/is/license.txt).

## Microsoft .NET runtime

The self-contained release includes components of the Microsoft .NET
runtime. Those components have their own license and third-party notices;
the app's MIT license does not replace them.

The installer copies `DOTNET-LICENSE.txt` and
`DOTNET-THIRD-PARTY-NOTICES.txt` into the app's `legal` folder, taken from
the .NET installation used to publish the release. Consult those files for
the terms and acknowledgments applicable to the bundled runtime.

Copyright (c) .NET Foundation and Contributors. Licensed under the MIT
License:

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## Island Control Center (IslandSettings)

The Control Center is built with React Native for Windows. The JavaScript
bundle is compiled to Hermes bytecode and the native binaries are shipped
alongside `IslandSettings.exe`.

- [React Native](https://github.com/facebook/react-native),
  [React](https://github.com/facebook/react) and
  [Hermes](https://github.com/facebook/hermes) — Copyright (c) Meta
  Platforms, Inc. and affiliates. MIT License.
- [React Native Windows](https://github.com/microsoft/react-native-windows) —
  Copyright (c) Microsoft Corporation. MIT License.

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## Island (main app)

The animated X icon adapts the two-stroke hover animation from
[Lucide Animated](https://github.com/pqoqubbw/icons) by dmytro (@pqoqubbw),
licensed under the MIT License:

```
MIT License

Copyright (c) 2024 pqoqubbw

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```
