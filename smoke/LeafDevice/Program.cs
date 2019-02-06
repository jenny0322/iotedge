// Copyright (c) Microsoft. All rights reserved.
namespace LeafDeviceTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    [Command(
        Name = "LeafDevice",
        Description = "An app which installs a certificate into cert store, creates a device and sends message and check if the message was received. This is to be used on a transparent Gateway setup for testing.",
        ExtendedHelpText = @"
Environment Variables:
  Most options in this command override environment variables. In other words,
  the value of the corresponding environment variable will be used unless the
  option is specified on the command line.

  Option                    Environment variable
  --connection-string       iothubConnectionString
  --eventhub-endpoint       eventhubCompatibleEndpointWithEntityPath

Defaults:
  All options to this command have defaults. If an option is not specified and
  its corresponding environment variable is not defined, then the default will
  be used.

  Option                    Default value
  --connection-string       get the value from Key Vault
  --eventhub-endpoint       get the value from Key Vault
  --device-id               an auto-generated unique identifier
  --certificate             Empty String.
  --edge-hostname           Empty String.
")]
    [HelpOption]
    class Program
    {
        [Option("-c|--connection-string <value>", Description = "Device connection string (hub-scoped, e.g. iothubowner)")]
        public string DeviceConnectionString { get; } = Environment.GetEnvironmentVariable("iothubConnectionString");

        [Option("-e|--eventhub-endpoint <value>", Description = "Event Hub-compatible endpoint for IoT Hub, including EntityPath")]
        public string EventHubCompatibleEndpointWithEntityPath { get; } = Environment.GetEnvironmentVariable("eventhubCompatibleEndpointWithEntityPath");

        [Option("-ct|--certificate <value>", Description = "Trust bundle CA Certificate(s) file to be installed on the machine.")]
        public string TrustedCACertificateFileName { get; } = string.Empty;

        [Option("-d|--device-id", Description = "Leaf device identifier to be registered with IoT Hub")]
        public string DeviceId { get; } = $"leaf-device--{Guid.NewGuid()}";

        [Option("-ed|--edge-hostname", Description = "Hostname of the Edge device that acts as a gateway to the leaf device")]
        public string EdgeHostName { get; } = string.Empty;

        [Option("-ed-id|--edge-device-id", Description = "Device Id of the Edge device that acts as a gateway to the leaf device. If not provided, the leaf device will not be in the Edge device's scope")]
        public string EdgeGatewayDeviceId { get; } = string.Empty;

        [Option("--use-web-sockets", CommandOptionType.NoValue, Description = "Use websockets for IoT Hub connections.")]
        public bool UseWebSockets { get; } = false;

        [Option("-cac|--x509-ca-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format to be used for X.509 CA authentication.")]
        public string X509CACertPath { get; } = string.Empty;

        [Option("-cak|--x509-ca-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format to be used for X.509 CA authentication.")]
        public string X509CAKeyPath { get; } = string.Empty;

        [Option("-ctpc|--x509-primary-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format. This is needed for thumbprint auth and used as the primary certificate.")]
        public string X509PrimaryCertPath { get; } = string.Empty;

        [Option("-ctpk|--x509-primary-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format. This is needed for thumbprint auth and used as the primary certificate's key.")]
        public string X509PrimaryKeyPath { get; } = string.Empty;

        [Option("-ctsc|--x509-secondary-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format. This is needed for thumbprint auth and used as the secondary certificate.")]
        public string X509SecondaryCertPath { get; } = string.Empty;

        [Option("-ctsk|--x509-secondary-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format. This is needed for thumbprint auth and used as the secondary certificate's key.")]
        public string X509SecondaryKeyPath { get; } = string.Empty;

        // ReSharper disable once UnusedMember.Local
        static int Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args).Result;

        // ReSharper disable once UnusedMember.Local
        async Task<int> OnExecuteAsync()
        {
            try
            {
                string connectionString = this.DeviceConnectionString ??
                                          await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey").ConfigureAwait(false);

                string endpoint = this.EventHubCompatibleEndpointWithEntityPath ??
                                  await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey").ConfigureAwait(false);

                var builder = new LeafDevice.LeafDeviceBuilder(
                    connectionString,
                    endpoint,
                    this.DeviceId,
                    this.TrustedCACertificateFileName,
                    this.EdgeHostName,
                    this.EdgeGatewayDeviceId,
                    this.UseWebSockets);

                if (!string.IsNullOrWhiteSpace(this.X509PrimaryCertPath) &&
                    !string.IsNullOrWhiteSpace(this.X509PrimaryKeyPath) &&
                    !string.IsNullOrWhiteSpace(this.X509SecondaryCertPath) &&
                    !string.IsNullOrWhiteSpace(this.X509SecondaryKeyPath))
                {
                    // use thumbprint auth and perform test for both primary and secondary certificates
                    var thumbprintCerts = new List<string> { this.X509PrimaryCertPath, this.X509SecondaryCertPath };
                    builder.SetX509ThumbprintAuthProperties(
                        this.X509PrimaryCertPath,
                        this.X509PrimaryKeyPath,
                        this.X509SecondaryCertPath,
                        this.X509SecondaryKeyPath,
                        true);
                    LeafDevice testPrimaryCertificate = builder.Build();
                    await testPrimaryCertificate.RunAsync().ConfigureAwait(false);

                    builder.SetX509ThumbprintAuthProperties(
                        this.X509PrimaryCertPath,
                        this.X509PrimaryKeyPath,
                        this.X509SecondaryCertPath,
                        this.X509SecondaryKeyPath,
                        false);
                    LeafDevice testSeondaryCertificate = builder.Build();
                    await testSeondaryCertificate.RunAsync().ConfigureAwait(false);
                }
                else if (!string.IsNullOrWhiteSpace(this.X509CACertPath) &&
                         !string.IsNullOrWhiteSpace(this.X509CAKeyPath))
                {
                    // use X.509 CA auth and perform test using CA chained certificates
                    builder.SetX509CAAuthProperties(
                        this.X509CACertPath,
                        this.X509CAKeyPath);
                    LeafDevice testCa = builder.Build();
                    await testCa.RunAsync().ConfigureAwait(false);
                }
                else
                {
                    builder.Build();
                    // non certificate flow use SAS tokens
                    LeafDevice testSas = builder.Build();
                    await testSas.RunAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }

            Console.WriteLine("Success!");
            return 0;
        }
    }
}
