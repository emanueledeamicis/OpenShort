# Third-party notices

OpenShort is released under the [MIT License](LICENSE). The Docker image also contains the third-party components listed here, each under its own license.

The exact versions are the ones resolved from `backend/src/*/*.csproj` and `frontend/package-lock.json` at build time. Inside the image, this file and the license texts mentioned below are in `/app/licenses`.

The complete source code of OpenShort is at <https://github.com/emanueledeamicis/OpenShort>.

## Backend (.NET)

| Component | Author | License |
|---|---|---|
| .NET runtime and ASP.NET Core (base image) | Microsoft | MIT |
| Entity Framework Core, Microsoft.Data.Sqlite, ASP.NET Core Identity, JwtBearer authentication, Microsoft.Extensions.*, Microsoft.Bcl.Cryptography, System.Configuration.ConfigurationManager, System.Security.*, System.Windows.Extensions | Microsoft | MIT |
| Microsoft.IdentityModel.*, System.IdentityModel.Tokens.Jwt | Microsoft | MIT |
| Microsoft.OpenApi | Microsoft | MIT |
| Swashbuckle.AspNetCore | domaindrivendev | MIT |
| Swagger UI (embedded in Swashbuckle.AspNetCore) | SmartBear Software | Apache-2.0 |
| SQLitePCLRaw | Eric Sink | Apache-2.0 |
| SQLite (native library bundled by SQLitePCLRaw) | SQLite authors | Public domain |
| MySql.EntityFrameworkCore (MySQL Connector/NET) | Oracle Corporation | GPL-2.0-only WITH Universal-FOSS-exception-1.0 |
| MySql.Data (MySQL Connector/NET) | Oracle Corporation | GPL-2.0-only WITH Universal-FOSS-exception-1.0 |
| BouncyCastle.Cryptography | Legion of the Bouncy Castle Inc. | MIT |
| Google.Protobuf | Google Inc. | BSD-3-Clause |
| K4os.Compression.LZ4, K4os.Compression.LZ4.Streams, K4os.Hash.xxHash | Milosz Krajewski | MIT |
| ZstdSharp.Port | Oleg Stepanischev | MIT |
| MIT Kerberos libraries for Windows (shipped inside MySql.Data, not used on Linux) | Massachusetts Institute of Technology and others | MIT-style, see the license file of the connector |

### MySQL Connector/NET

`MySql.EntityFrameworkCore` 10.0.12 and `MySql.Data` 26.10.0 are distributed unmodified, as published by Oracle on NuGet, under the GNU General Public License version 2 with the Universal FOSS Exception, version 1.0. They remain under that license.

OpenShort uses the connector only through its public interfaces, without copying any of its code, and is itself distributed with complete source under the MIT License at the address above. For this combination OpenShort relies on the Universal FOSS Exception.

- License text (GPL version 2, the exception, and the notices of the components the connector includes): `/app/licenses/mysql-connector-net-LICENSE.txt` inside the image, the `LICENSE` file of the NuGet package
- Source code of the distributed version: <https://github.com/mysql/mysql-connector-net/tree/26.10.0>

The components in the rows after them in the table come with the connector. Some of the Microsoft packages in the second row do as well.

## Frontend (dashboard)

The dashboard is compiled into JavaScript and CSS bundles that include code from these packages. Their full license texts are in `/app/licenses/frontend-3rdpartylicenses.txt` inside the image, written by the Angular build.

| Component | Author | License |
|---|---|---|
| Angular (`@angular/*`) | Google LLC | MIT |
| PrimeNG (`primeng`, `@primeng/themes`), community version | PrimeTek | MIT |
| PrimeUIX (`@primeuix/*`) | PrimeTek | MIT |
| PrimeIcons | PrimeTek | MIT |
| RxJS | RxJS contributors | Apache-2.0 |
| tslib | Microsoft | 0BSD |
| qrcode-generator | Kazuhiko Arase | MIT |
| Tailwind CSS (generated styles) | Tailwind Labs | MIT |

## System packages in the image

The image is built on Microsoft's ASP.NET Core runtime image and adds two packages from its Linux distribution:

| Component | License |
|---|---|
| nginx | BSD-2-Clause |
| Supervisor | BSD-derived (Supervisor license); some of its files are under other licenses, listed in its copyright file |

The license of every system package, including the ones of the base image and the dependencies of these two, is in `/usr/share/doc/<package>/copyright` inside the image.
