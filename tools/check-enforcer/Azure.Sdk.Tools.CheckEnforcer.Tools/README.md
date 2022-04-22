**In ../Azure.Sdk.Tools.CheckEnforcer**

```
func start
```

**In current directory**

To trigger a payload event against the local function host, with the same data that would normally come through eventhubs:

```
dotnet run -- trigger -n webhook-eventhubs -f ./payload.json
```
