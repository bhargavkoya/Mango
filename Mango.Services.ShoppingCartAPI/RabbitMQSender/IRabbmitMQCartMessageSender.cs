namespace Mango.Services.ShoppingCartAPI.RabbitMQSender
{
    public interface IRabbmitMQCartMessageSender
    {
        void SendMessage(Object message, string queueName);
    }
}
