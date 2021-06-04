using System.Text;
using System.Text.Json;
using newkilibraries;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace newki_inventory_customer
{
    public interface IRabbitMqService
    {
        void CreateQueues(string queueName);
        void Enqueue(InventoryMessage message);
        void Close();
    }
    public class RabbitMqService : IRabbitMqService
    {
        private IConnection _connection;
        private IModel _channel;
        private string _queueName;
        private EventingBasicConsumer _consumer;

        public RabbitMqService()
        {
        }

        public void CreateQueues(string queueName)
        {
            ConnectionFactory factory = new ConnectionFactory();
            factory.UserName = "user";
            factory.Password = "password";
            factory.HostName = "localhost";

            _connection = factory.CreateConnection();

            _queueName = queueName;
            using (var channel = _connection.CreateModel())
            {
                channel.QueueDeclare(_queueName, false, false, false);
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (ch, ea) =>
                {
                    var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var updateCustomerFullNameModel = JsonSerializer.Deserialize<InventoryMessage>(content);

                    HandleMessage(updateCustomerFullNameModel);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }; ;
                channel.BasicConsume(queue: _queueName,
                       autoAck: true,
                       consumer: consumer);
            }

        }

        private void HandleMessage(InventoryMessage updateCustomerFullNameModel)
        {
            var temp = updateCustomerFullNameModel;
        }

        public void Enqueue(InventoryMessage message)
        {
            using (var channel = _connection.CreateModel())
            {
                channel.QueueDeclare(_queueName, false, false, false);
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                channel.BasicPublish(exchange: string.Empty,
                                routingKey: _queueName,
                                basicProperties: null,
                                body: body);
            }
        }

        public void Close()
        {
            _connection.Close();
        }
    }
}