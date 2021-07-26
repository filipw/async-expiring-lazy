using System;
using System.Threading.Tasks;
using Strathweb;
using Xunit;

namespace AsyncExpiringLazy.Tests
{
    public class EagerExpiringLazyTests
    {
        [Fact]
        public async Task End2End()
        {
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

            // 10. fetch token once again
            var token4 = await testInstance.Value();

            // 11. check if value is created - it should be created once again
            Assert.True(testInstance.IsValueCreated());

            // 12. check if token fetched after invalidation is different than older one
            Assert.NotSame(token3, token4);

            // 13. Dispose
            testInstance.Dispose();
            
            // 14. check if value does not exist after disposal
            Assert.False(testInstance.IsValueCreated());

            // 15. Getting value should throw exception because internals are disposed
            await Assert.ThrowsAsync<ObjectDisposedException>(testInstance.Value);
        }

        [Fact]
        public async Task HandlesExceptionOnFirstCall()
        {
            using var testInstance = new AsyncExpiringEager<TokenResponse>(_ => throw new Exception());
            await Assert.ThrowsAsync<Exception>(testInstance.Value);
        }

        [Fact]
        public async Task HandlesExceptionsFromNextIterations()
        {
            int counter = 0;
            using var testInstance = new AsyncExpiringEager<TokenResponse>(_ =>
            {
                counter++;
                throw new Exception(counter.ToString());
            });
            
            // 1. Sleep, new token should be retrieved. But this time call should fail and exception should be received
            var exception1 = await Assert.ThrowsAsync<Exception>(testInstance.Value);

            // 2. Sleep for next seconds, we should still receive old exception
            await Task.Delay(2000);
            var exception2 = await Assert.ThrowsAsync<Exception>(testInstance.Value);
            Assert.Same(exception1, exception2);
        }
    }
}
