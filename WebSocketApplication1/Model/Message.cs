using WebSocketApplication1.Common;

namespace WebSocketApplication1.Model
{
    public class Message
    {
        /// <summary>
        /// 发送的用户
        /// </summary>
        public string SenderUser { get; set; } = string.Empty;
        /// <summary>
        /// 目标的用户
        /// </summary>
        public string TargetUser { get; set; } = string.Empty;
        /// <summary>
        /// 发送的服务器地址
        /// </summary>
        public string ServerAddress { get; set; } = string.Empty;
        /// <summary>
        /// 接收人(个体或群组)
        /// </summary>
        public MessageType SenderType { get; set; } = MessageType.Individual;
        /// <summary>
        /// 信息内容
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// 信息时间
        /// </summary>
        public DateTime MssageTime { get; set; } = DateTime.Now;
    }
}
