# Third-party notices

`EmailIndexer.exe` embeds the following open-source components (merged into the single exe by Costura.Fody). Versions are those pinned in the project files.

| Component | Version | License | Source |
|---|---|---|---|
| MimeKit | 4.18.1 | MIT | https://github.com/jstedfast/MimeKit |
| MsgReader | 6.1.2 | MIT | https://github.com/Sicos1977/MSGReader |
| BouncyCastle.Cryptography (via MimeKit) | 2.7.0 | MIT | https://github.com/bcgit/bc-csharp |
| OpenMcdf (via MsgReader) | 3.3.0 | MPL-2.0 | https://github.com/ironfede/openmcdf |
| UTF.Unknown (via MsgReader) | 2.7.0 | MPL-1.1 | https://github.com/CharsetDetector/UTF-unknown |
| RtfPipe (via MsgReader) | 2.0 | MIT | https://github.com/erdomke/RtfPipe |
| Costura.Fody (build time) | 5.7.0 | MIT | https://github.com/Fody/Costura |
| Microsoft .NET libraries (System.Memory, System.Text.Json, System.Text.Encoding.CodePages, System.Security.Cryptography.Pkcs, …) | – | MIT | https://github.com/dotnet/runtime |

OpenMcdf and UTF.Unknown are used unmodified. Under the Mozilla Public License their source code is available at the links above; the MPL applies only to those components, not to the rest of this program.

Tests additionally use xUnit (Apache-2.0) and MsgKit (MIT); these are not shipped.
