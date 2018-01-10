$connectionLimits = @(
    1,
    2,
    10,
    50,
    100
)
$methods = @(
    0,
    1,
    2
)
$env:Logging:LogLevel:Microsoft="None"
foreach($climit in $connectionLimits)
{
    foreach($method in $methods)
    {
        $env:MTHC_CONNECTION_LIMIT=$climit
        dotnet run -c Release $method > mthc-test-result-net-$climit-$method.txt
    }
}