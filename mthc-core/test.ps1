$connectionLimits = @(
    1,
    2,
    10,
    50,
    100
)
$methods = @(
    1,
    2
)
$env:Logging:LogLevel:Microsoft="None"
$env:MTHC_LOOP_NUM=100
foreach($climit in $connectionLimits)
{
    foreach($method in $methods)
    {
        $env:MTHC_CONNECTION_LIMIT=$climit
        dotnet run -c Release $method > mthc-test-result-$climit-$method.txt
    }
}