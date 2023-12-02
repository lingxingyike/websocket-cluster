using System.Net;
using RedisHelper;
using RedisHelper.Interface;
using WebSocketApplication1;

var builder = WebApplication.CreateBuilder(args);
//注册分布式锁 Redis模式

//builder.Services.AddSingleton<IDistributedLock, RedisLock>();
//builder.Services.AddSingleton<IRedisHelper, RedisHelper.RedisHelper>();

builder.Services.AddRedisLock(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redisConnection")!;
    options.InstanceName = "lock";
});
builder.Services.AddRedisHelper(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("redisConnection")!;
    options.DbNumber = 0;
});

builder.WebHost.UseUrls("http://0.0.0.0:6969");
var app = builder.Build();
app.UseWebSockets();
app.MapGet("/ws", async (HttpContext context, IRedisHelper redisHelper,IDistributedLock distLock) => {

    var serverAddress = builder.Configuration.GetConnectionString("serverAddress")!;
    if (context.WebSockets.IsWebSocketRequest)
    {
        WebSocketHandler handler = new WebSocketHandler();
        var user = context.Request.Query["user"].ToString();
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        await handler.ProcessChat(ws, user, serverAddress, redisHelper, distLock);
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

//app.MapGet("/ws", async context => {
//    if (context.WebSockets.IsWebSocketRequest)
//    {
//        WebSocketHandler handler = new WebSocketHandler();
//        var user = context.Request.Query["user"].ToString();
//        using var ws = await context.WebSockets.AcceptWebSocketAsync();
//        await handler.ProcessChat(ws, user);
//    }
//    else
//    {
//        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
//    }
//});
/*
app.MapGet("/ws", async context => {
    if (context.WebSockets.IsWebSocketRequest)
    {
        var buffer = new byte[1024 * 4];
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        while (ws.State == WebSocketState.Open)
        {
            var message = "The current tiem is:" + DateTime.Now.ToString("HH:mm:ss");
            var bytes = Encoding.UTF8.GetBytes(message);
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

            await ws.SendAsync(arraySegment,
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);

            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            //关闭连接
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.Empty, string.Empty, CancellationToken.None);//如果client发起close请求,对client进行ack
                break;
            }
            var str = Encoding.UTF8.GetString(buffer);
            var bytes2 = Encoding.UTF8.GetBytes("You say:" + str);
            var arraySegment2 = new ArraySegment<byte>(bytes2, 0, bytes2.Length);
            await ws.SendAsync(arraySegment2,
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
            Thread.Sleep(1000);
        }
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});
*/

await app.RunAsync();