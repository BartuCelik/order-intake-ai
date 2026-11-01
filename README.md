# Order Intake AI

Turns PDF purchase orders that arrive by e-mail into structured JSON, so a Power Automate flow can create the
order in the [Order & Inventory Panel](https://github.com/BartuCelik/order-inventory-panel).

Work in progress.

## Run locally

Requires the .NET 10 SDK.

```sh
cd src/OrderIntake.Api
dotnet user-secrets set ApiKey:Key "<at least 32 characters>"
dotnet run            # http://localhost:5280/health
```

Every endpoint except `/health` and the OpenAPI document needs the key in the `X-Api-Key` header.

```sh
dotnet test           # from the repository root
```
