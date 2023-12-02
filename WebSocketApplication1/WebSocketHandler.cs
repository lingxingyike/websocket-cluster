using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using RedisHelper.Interface;
using System.Net.WebSockets;
using System.Text;
using System;
using System.Net.Sockets;
using System.Threading.Channels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketApplication1.Model;

namespace WebSocketApplication1
{
    /// <summary>
    /// WebSocketHandler 的摘要说明
    /// </summary>
    public class WebSocketHandler
    {
        private static string prefixQueueName = "user_queue";
        private static Dictionary<string, WebSocket> SOCKET_POOL = new Dictionary<string, WebSocket>();//用户连接池
        private static Dictionary<string, IModel?> CHANNEL_POOL = new Dictionary<string, IModel?>();//MQ Channel
        private static ConnectionFactory factory = new ConnectionFactory()
        {
            HostName = "124.222.236.109",
            Port = 5672,
            UserName = "dzm",
            Password = "123456",
            //VirtualHost = "test",
        };
        private static IConnection connection = factory.CreateConnection();
        public async void ProcessRequest(HttpContext context, IRedisHelper redisHelper, IDistributedLock distLock)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var user = context.Request.Query["user"].ToString();
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var serverAddress = "serverAddress";
                await ProcessChat(socket, user, serverAddress, redisHelper, distLock);
            }
        }
        /// <summary>
        /// 聊天进程
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="user"></param>
        /// <param name="serverAddress"></param>
        /// <param name="redisHelper"></param>
        /// <param name="distLock"></param>
        /// <returns></returns>
        public async Task ProcessChat(WebSocket socket, string user,string serverAddress, IRedisHelper redisHelper, IDistributedLock distLock)
        {
            var channelKey = $"{prefixQueueName}:{user}";
            var socketKey = user;
            try
            {
                #region 用户添加连接池
                //第一次open时，添加到连接池中
                if (!SOCKET_POOL.ContainsKey(socketKey))
                    SOCKET_POOL.Add(socketKey, socket);//不存在，添加
                else
                    if (socket != SOCKET_POOL[socketKey])//当前对象不一致，更新
                    SOCKET_POOL[socketKey] = socket;
                #endregion

                #region 订阅MQ
                SubOfflineMessage(channelKey, socketKey);
                #endregion


                string descUser = string.Empty;//目的用户
                while (socket.State == WebSocketState.Open)
                {
                    var buffer = new byte[1024 * 4];
                    ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer);
                    var result = await socket.ReceiveAsync(arraySegment, CancellationToken.None);

                    #region 消息处理（字符截取、消息转发）
                    try
                    {
                        #region 关闭Socket处理，删除连接池
                        if (socket.State != WebSocketState.Open)//连接关闭
                        {
                            if (SOCKET_POOL.ContainsKey(socketKey)) SOCKET_POOL.Remove(socketKey);//删除连接池
                            break;
                        }
                        #endregion

                        string userMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);//发送过来的消息
                        Message msg = JsonConvert.DeserializeObject<Message>(userMsg);

                        if (!string.IsNullOrEmpty(msg.TargetUser))
                        {
                            descUser = msg.TargetUser;//记录消息目的用户
                            arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userMsg));
                        }
                        else
                        {
                            arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userMsg));
                        }

                        if (SOCKET_POOL.ContainsKey(descUser))//判断客户端是否在线
                        {
                            WebSocket destSocket = SOCKET_POOL[descUser];//目的客户端
                            if (destSocket != null && destSocket.State == WebSocketState.Open)
                                await destSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        else
                        {
                            Task.Run(() =>
                            {
                                var desQueueName = $"{prefixQueueName}:{descUser}";
                                PubMQMessage(desQueueName, userMsg);//将用户添加至离线消息池中
                            });
                        }
                    }
                    catch (Exception exs)
                    {
                        //消息转发异常处理，本次消息忽略 继续监听接下来的消息
                    }
                    #endregion
                }//while end
            }
            catch (Exception ex)
            {
            }
            finally
            {
                var channel = CHANNEL_POOL.ContainsKey($"{prefixQueueName}:{user}") ? CHANNEL_POOL[$"{prefixQueueName}:{user}"]
                    : connection.CreateModel();
                channel?.Dispose();
                if (CHANNEL_POOL.ContainsKey(channelKey)) CHANNEL_POOL.Remove(channelKey);
                //整体异常处理
                if (SOCKET_POOL.ContainsKey(user)) SOCKET_POOL.Remove(user);
            }
            
        }

        /// <summary>
        /// 订阅离线信息
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="socketKey"></param>
        private void SubOfflineMessage(string queueName,string socketKey)
        {
            var channel = connection.CreateModel();
            channel.QueueDeclare(queue: queueName,
                                    durable: true,
                                    exclusive: false,
                                    autoDelete: false,
                                    arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                //在当前服务找到目标的WebSocket连接并发送消息

                if (SOCKET_POOL.ContainsKey(socketKey))
                {
                    var targetSocket = SOCKET_POOL[socketKey];
                    if (targetSocket.State == WebSocketState.Open)
                    {
                        var arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                        await targetSocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            };
            channel.BasicConsume(queue: queueName,
                                    autoAck: false,
                                    consumer: consumer);
            CHANNEL_POOL.Add(queueName, channel);
        }
        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="message"></param>
        private void PubMQMessage(string queueName, string message)
        {
            var channel = CHANNEL_POOL.ContainsKey(queueName) ? CHANNEL_POOL[queueName] 
                : connection.CreateModel();

            if (CHANNEL_POOL.ContainsKey(queueName))
                CHANNEL_POOL.Add(queueName, channel);

            channel.QueueDeclare(queue: queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            channel.BasicPublish(exchange: "",
                                 routingKey: queueName,
                                 basicProperties: properties,
                                 body: body);
        }
    }

}
