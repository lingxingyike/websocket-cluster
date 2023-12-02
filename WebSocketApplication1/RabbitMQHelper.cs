using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

namespace WebSocketApplication1
{
    /// <summary>
    /// </summary>
    public class RabbitMQHelper
    {
        private static ConnectionFactory factory;
        private static object lockObj = new object();
        /// <summary>
        /// 获取单个RabbitMQ连接
        /// </summary>
        /// <returns></returns>
        public static IConnection GetConnection()
        {
            if (factory == null)
            {
                lock (lockObj)
                {
                    if (factory == null)
                    {
                        factory = new ConnectionFactory
                        {
                            HostName = "124.222.236.109",//ip
                            Port = 15672,//端口
                            UserName = "guest",//账号
                            Password = "guest",//密码
                            //VirtualHost = "develop" //虚拟主机
                        };
                    }
                }
            }
            return factory.CreateConnection();
        }
        /// <summary>
        /// 发布消息
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        public static void PubExchangeMessage(string exchangeName, string message)
        {

            var connFactory = new ConnectionFactory()
            {
                HostName = "192.168.178.128",
                Port = 5672,
                UserName = "dzm",
                Password = "123456",
                VirtualHost = "test",
            };
            using (IConnection conn = connFactory.CreateConnection())
            {
                using (IModel channel = conn.CreateModel())
                {

                    //声明交换机
                    channel.ExchangeDeclare(exchange: exchangeName, type: "fanout");
                    while (true)
                    {
                        Console.WriteLine("消息内容:");
                        //消息内容
                        byte[] body = Encoding.UTF8.GetBytes(message);

                        //发送消息
                        channel.BasicPublish(exchange: exchangeName, routingKey: "", basicProperties: null, body: body);

                        Console.WriteLine("成功发送消息:" + message);
                    }
                }
            }
        }
        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="exchangeName"></param>
        public static void SubExchangeMessage(string userId, string exchangeName)
        {

            var connFactory = new ConnectionFactory()
            {
                HostName = "192.168.178.128",
                Port = 5672,
                UserName = "dzm",
                Password = "123456",
                VirtualHost = "test",
            };
            using (IConnection conn = connFactory.CreateConnection())
            {
                using (IModel channel = conn.CreateModel())
                {
                    //声明交换机
                    channel.ExchangeDeclare(exchange: exchangeName, type: "fanout");

                    //消息队列名称
                    String queueName = exchangeName + ":" + userId;

                    //声明队列
                    channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

                    //将队列与交换机进行绑定
                    channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: "");

                    //声明为手动确认
                    channel.BasicQos(0, 1, false);

                    //定义消费者
                    var consumer = new EventingBasicConsumer(channel);

                    //接收事件
                    consumer.Received += (model, ea) =>
                    {
                        byte[] message = ea.Body.ToArray();//接收到的消息
                        Console.WriteLine("接收到信息为:" + Encoding.UTF8.GetString(message));

                        //返回消息确认
                        channel.BasicAck(ea.DeliveryTag, true);

                    };
                    //开启监听
                    channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                    Console.ReadKey();
                }
            }
        }

    }
}
