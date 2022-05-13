using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using Serilog;

namespace Api.Services
{
    class MqttService : BackgroundService
    {
        private readonly ILogger<MqttService> _logger;

        private readonly MqttFactory _mqttFactory;

        private readonly IManagedMqttClient _mqttClient;

        private readonly ManagedMqttClientOptions _options;

        public MqttService(ILogger<MqttService> logger, IConfiguration config, KeyVaultService keyVault)
        {
            _logger = logger;
            _mqttFactory = new MqttFactory();
            string username = config.GetSection("Mqtt").GetValue<string>("Username");
            //string password = keyVault.GetSecret("MQTT-BROKER-PASSWORD").Result;
            string password = "default";

            MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder()
                                        .WithTcpServer(config.GetSection("Mqtt").GetValue<string>("Host"), config.GetSection("mqtt").GetValue<int>("port")).
                                        WithCredentials(username, password);

            _options = new ManagedMqttClientOptionsBuilder()
                                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(60))
                                    .WithClientOptions(builder.Build())
                                    .Build();
            _mqttClient = _mqttFactory.CreateManagedMqttClient();

            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _mqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(OnConnectingFailed);

            _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(a =>
            {
                _logger.LogInformation("Message recieved: {payload}", a.ApplicationMessage.ConvertPayloadToString());
            });

            var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(f => { f.WithTopic("#"); }).Build();
            _mqttClient.SubscribeAsync(mqttSubscribeOptions.TopicFilters);

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingtoken)
        {
            await _mqttClient.StartAsync(_options);
        }

        public void OnConnected(MqttClientConnectedEventArgs obj)
        {
            _logger.LogInformation("Successfully connected.");
        }

        public void OnConnectingFailed(ManagedProcessFailedEventArgs obj)
        {
            _logger.LogWarning($"Couldn't connect to broker. Exception: {obj.Exception.Message}");
        }

        public void OnDisconnected(MqttClientDisconnectedEventArgs obj)
        {
            _logger.LogInformation("Successfully disconnected.");
        }
    }
}
