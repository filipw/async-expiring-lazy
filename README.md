# Async Expiring Lazy


See [blog article](https://www.strathweb.com/2016/11/lazy-async-initialization-for-expiring-objects/) for more background details and a [follow up post](https://www.strathweb.com/2021/07/eager-refresh-of-values-for-asyncexpiringlazy/) for further update.

## Installation

Grab the Nuget package called [Strathweb.AsyncExpiringLazy](https://www.nuget.org/packages/Strathweb.AsyncExpiringLazy/). It is cross compiled for .NET Standard 1.1, .NET Standard 2.0 and .NET Framework 4.6.1 and compatible with:

 - .NET Framework 4.6.1
 - all version of .NET Core
 - Xamarin
 - Mono
 - Universal Windows Platform
 
Latest version: [![Nuget](http://img.shields.io/nuget/v/Strathweb.AsyncExpiringLazy.svg?maxAge=10800)](https://www.nuget.org/packages/Strathweb.AsyncExpiringLazy/)

## Usage

The code sample shows creating a lazy epxiring value with `AsyncExpiringLazy<T>` and letting it get recreated on first reuse after expiration.

```csharp
var testInstance = new AsyncExpiringLazy<TokenResponse>(async metadata =>
{
    await Task.Delay(1000);
    return new ExpirationMetadata<TokenResponse>
    {
        Result = new TokenResponse
        {
            AccessToken = Guid.NewGuid().ToString()
        }, ValidUntil = DateTimeOffset.UtcNow.AddSeconds(2)
    };
});

// 1. check if value is created - shouldn't
Assert.False(await testInstance.IsValueCreated());

// 2. fetch lazy expiring value
var token = await testInstance.Value();

// 3a. verify it is created now
Assert.True(await testInstance.IsValueCreated());

// 3b. verify it is not null
Assert.NotNull(token.AccessToken);

// 4. fetch the value again. Since it's lifetime is 2 seconds, it should be still the same
var token2 = await testInstance.Value();
Assert.Same(token, token2);

// 5. sleep for 2 seconds to let the value expire
await Task.Delay(2000);

// 6a. verify the value expired
Assert.False(await testInstance.IsValueCreated());

// 6b. fetch again
var token3 = await testInstance.Value();

// 7. verify we now have a new (recreated) value - as the previous one expired
Assert.NotSame(token2, token3);

// 8. invalidate the value manually before it has a chance to expire
await testInstance.Invalidate();

// 9. check if value is created - shouldn't anymore
Assert.False(await testInstance.IsValueCreated());
```

The next code sample shows creating a lazy epxiring value with `AsyncExpiringEager<T>` and letting it get recreated silently as soon as it expires.
Pay attetion to the different from the previous example at step `6a`.

```csharp
using var testInstance = new AsyncExpiringEager<TokenResponse>(async metadata =>
{
    await Task.Delay(1000);
    return new ExpirationMetadata<TokenResponse>
    {
        Result = new TokenResponse
        {
            AccessToken = Guid.NewGuid().ToString()
        }, ValidUntil = DateTimeOffset.UtcNow.AddSeconds(2)
    };
});

// 1. check if value is created - shouldn't
Assert.False(testInstance.IsValueCreated());

// 2. fetch lazy expiring value
var token = await testInstance.Value();

// 3a. verify it is created now
Assert.True(testInstance.IsValueCreated());

// 3b. verify it is not null
Assert.NotNull(token.AccessToken);

// 4. fetch the value again. Since it's lifetime is 2 seconds, it should be still the same
var token2 = await testInstance.Value();
Assert.Same(token, token2);

// 5. sleep for 2 seconds to let the value expire
await Task.Delay(2000);

// 6a. verify the value was eagerly created just before it expired
Assert.True(testInstance.IsValueCreated());

// 6b. fetch again
var token3 = await testInstance.Value();

// 7. verify we now have a new (recreated) value - as the previous one expired
Assert.NotSame(token2, token3);

// 8. invalidate the value manually before it has a chance to expire
testInstance.Invalidate();

// 9. check if value is created - shouldn't anymore
Assert.False(testInstance.IsValueCreated());
```

## License

[MIT](https://github.com/filipw/async-expiring-lazy/blob/master/LICENSE)
