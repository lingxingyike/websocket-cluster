using RedisHelper.Model;
using Microsoft.Extensions.DependencyInjection;
using RedisHelper.Interface;

namespace RedisHelper
{
    public static class ServiceCollectionExtensions
    {
        public static void AddRedisLock(this IServiceCollection services, Action<RedisSetting> action)
        {
            services.Configure(action);
            services.AddSingleton<IDistributedLock, RedisLock>();
        }
    }
}
