using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using newki_inventory_customer;
using newki_inventory_vendor.Services;
using newkilibraries;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace newki_inventory_vendor
{
    class Program
    {
        static ManualResetEvent _quitEvent = new ManualResetEvent(false);
        private static string _connectionString;

        static void Main(string[] args)
        {
            //Reading configuration
            var Vendors = new List<Vendor>();
            var awsStorageConfig = new AwsStorageConfig();
            var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json", true, true);
            var Configuration = builder.Build();

            Configuration.GetSection("AwsStorageConfig").Bind(awsStorageConfig);


            var services = new ServiceCollection();

            _connectionString = Configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_connectionString));
            services.AddTransient<IAwsService, AwsService>();
            services.AddTransient<IVendorService, VendorService>();
            services.AddTransient<IRabbitMqService, RabbitMqService>();
            services.AddSingleton<IAwsStorageConfig>(awsStorageConfig);

            var serviceProvider = services.BuildServiceProvider();
            var rabbitMq = serviceProvider.GetService<IRabbitMqService>();

            InventoryMessage inventoryMessage;

            ConnectionFactory factory = new ConnectionFactory();
            factory.UserName = "user";
            factory.Password = "password";
            factory.HostName = "localhost";

            var connection = factory.CreateConnection();

            var requestQueueName = "VendorRequest";
            var responseQueueName = "VendorResponse";

            var channel = connection.CreateModel();
            channel.QueueDeclare(requestQueueName, false, false, false);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (ch, ea) =>
            {
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                var updateVendorFullNameModel = JsonSerializer.Deserialize<InventoryMessage>(content);

                ProcessRequest(updateVendorFullNameModel);

            }; ;
            channel.BasicConsume(queue: requestQueueName,
                   autoAck: true,
                   consumer: consumer);


            _quitEvent.WaitOne();

        }

        private static void ProcessRequest(InventoryMessage inventoryMessage)
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseNpgsql(_connectionString);

                using (var appDbContext = new ApplicationDbContext(optionsBuilder.Options))
                {
                    var vendorService = new VendorService(appDbContext);
                    var messageType = Enum.Parse<InventoryMessageType>(inventoryMessage.Command);

                    switch (messageType)
                    {
                        case InventoryMessageType.Search:
                            {
                                ProcessSearch(vendorService, appDbContext);
                                break;
                            }
                        case InventoryMessageType.Get:
                            {
                                Console.WriteLine("Loading a Vendor...");
                                var id = JsonSerializer.Deserialize<int>(inventoryMessage.Message);
                                var Vendor = vendorService.GetVendor(id);
                                var content = JsonSerializer.Serialize(Vendor);

                                var responseMessageNotification = new InventoryMessage();
                                responseMessageNotification.Command = InventoryMessageType.Get.ToString();
                                responseMessageNotification.RequestNumber = inventoryMessage.RequestNumber;
                                responseMessageNotification.MessageDate = DateTimeOffset.UtcNow;

                                var inventoryResponseMessage = new InventoryMessage();
                                inventoryResponseMessage.Message = content;
                                inventoryResponseMessage.Command = inventoryMessage.Command;
                                inventoryResponseMessage.RequestNumber = inventoryMessage.RequestNumber;

                                Console.WriteLine("Sending the message back");

                                break;

                            }
                        case InventoryMessageType.Insert:
                            {
                                Console.WriteLine("Adding new Vendor");
                                var Vendor = JsonSerializer.Deserialize<Vendor>(inventoryMessage.Message);
                                vendorService.Insert(Vendor);
                                ProcessSearch(vendorService, appDbContext);
                                break;
                            }
                        case InventoryMessageType.Update:
                            {
                                Console.WriteLine("Updating a Vendor");
                                var Vendor = JsonSerializer.Deserialize<Vendor>(inventoryMessage.Message);
                                vendorService.Update(Vendor);
                                var existingVendor = appDbContext.VendorDataView.Find(Vendor.VendorId);
                                existingVendor.Data = JsonSerializer.Serialize(Vendor);
                                appDbContext.SaveChanges();
                                break;
                            }
                        case InventoryMessageType.Delete:
                            {
                                Console.WriteLine("Deleting a Vendor");
                                var id = JsonSerializer.Deserialize<int>(inventoryMessage.Message);
                                vendorService.Remove(id);
                                var removeVendor = appDbContext.VendorDataView.FirstOrDefault(predicate => predicate.VendorId == id);
                                appDbContext.VendorDataView.Remove(removeVendor);
                                appDbContext.SaveChanges();
                                break;
                            }
                        default: break;

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void ProcessSearch(IVendorService vendorService, ApplicationDbContext appDbContext)
        {
            Console.WriteLine("Loading all the Vendors...");
            var vendors = vendorService.GetVendors();

            foreach (var vendor in vendors)
            {
                if (appDbContext.VendorDataView.Any(p => p.VendorId == vendor.VendorId))
                {
                    var existingVendor = appDbContext.VendorDataView.Find(vendor.VendorId);
                    existingVendor.Data = JsonSerializer.Serialize(vendor);
                }
                else
                {
                    var VendorDataView = new VendorDataView
                    {
                        VendorId = vendor.VendorId,
                        Data = JsonSerializer.Serialize(vendor)
                    };
                    appDbContext.VendorDataView.Add(VendorDataView);
                }
                appDbContext.SaveChanges();
            }
        }
    }
}
