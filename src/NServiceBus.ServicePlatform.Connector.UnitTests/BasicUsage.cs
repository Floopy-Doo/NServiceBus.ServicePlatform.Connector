﻿namespace NServiceBus.PlatformConnection.UnitTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Configuration.AdvancedExtensibility;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Particular.Approvals;
    using Settings;

    [TestFixture]
    class BasicUsage
    {
        const string JsonConfiguration = @"{
            ""errorQueue"": ""myErrorQueue"",
            ""heartbeats"": {
                ""enabled"": true,
                ""heartbeatQueue"": ""heartbeatsServiceControlQueue""
            },
            ""customChecks"": {
                ""enabled"": true,
                ""customCheckQueue"": ""customChecksServiceControlQueue""
            },
            ""messageAudit"": {
                ""enabled"": true,
                ""auditQueue"": ""myAuditQueue""
            },
            ""sagaAudit"": {
                ""enabled"": true,
                ""sagaAuditQueue"": ""sagaAuditServiceControlQueue""
            },
            ""metrics"": {
                ""enabled"": true,
                ""metricsQueue"": ""metricServiceControlQueue"",
                ""interval"": ""00:00:10""
            }
        }";

        [Test]
        public void UpdatesConfiguration()
        {
            var connectionConfig = ServicePlatformConnectionConfiguration.Parse(JsonConfiguration);

            var endpointConfig = new EndpointConfiguration("SomeEndpoint");

            var beforeSettings = GetExplicitSettings(endpointConfig);

            endpointConfig.ConnectToServicePlatform(connectionConfig);

            var afterSettings = GetExplicitSettings(endpointConfig);
            var changes = afterSettings.Except(beforeSettings)
                .OrderBy(x => x)
                .ToArray();

            Approver.Verify(changes);
        }

        static IEnumerable<TestCaseData> OneEnabledFeature
        {
            get
            {
                var messageAudit = new ServicePlatformConnectionConfiguration
                {
                    MessageAudit = new ServicePlatformMessageAuditConfiguration
                    {
                        Enabled = true,
                        AuditQueue = "audit"
                    }
                };
                yield return new TestCaseData("MessageAudit", messageAudit);

                var customChecks = new ServicePlatformConnectionConfiguration
                {
                    CustomChecks = new ServicePlatformCustomChecksConfiguration
                    {
                        Enabled = true,
                        CustomCheckQueue = "CustomChecksQueue"
                    }
                };
                yield return new TestCaseData("CustomChecks", customChecks);

                var heartbeats = new ServicePlatformConnectionConfiguration
                {
                    Heartbeats = new ServicePlatformHeartbeatConfiguration
                    {
                        Enabled = true,
                        HeartbeatQueue = "HeartbeatQueue"
                    }
                };
                yield return new TestCaseData("Heartbeats", heartbeats);

                var sagaAudit = new ServicePlatformConnectionConfiguration
                {
                    SagaAudit = new ServicePlatformSagaAuditConfiguration
                    {
                        Enabled = true,
                        SagaAuditQueue = "SagaAuditQueue"
                    }
                };
                yield return new TestCaseData("SagaAudit", sagaAudit);

                var metrics = new ServicePlatformConnectionConfiguration
                {
                    Metrics = new ServicePlatformMetricsConfiguration
                    {
                        Enabled = true,
                        MetricsQueue = "MetricsQueue",
                        Interval = TimeSpan.FromSeconds(1)
                    }
                };
                yield return new TestCaseData("Metrics", metrics);
            }
        }

        [Test, TestCaseSource(nameof(OneEnabledFeature))]
        public void HandlesEnableFlag(string scenario, ServicePlatformConnectionConfiguration connectionConfig)
        {
            var endpointConfig = new EndpointConfiguration("SomeEndpoint");

            var beforeSettings = GetExplicitSettings(endpointConfig);

            endpointConfig.ConnectToServicePlatform(connectionConfig);

            var afterSettings = GetExplicitSettings(endpointConfig);
            var changes = afterSettings.Except(beforeSettings)
                .OrderBy(x => x)
                .ToArray();

            Approver.Verify(changes, scenario: scenario);
        }

        [Test]
        public void CanBeDeserializedByMicrosoftConfigurationApi()
        {
            var builder = new HostBuilder();

            IEnumerable<string> settingChanges = null;

            builder
                .ConfigureAppConfiguration(cb =>
                {
                    var json = $@"{{""ServicePlatformConfiguration"" : {JsonConfiguration}}}";

                    var jsonStream = new MemoryStream(Encoding.ASCII.GetBytes(json));
                    cb.AddJsonStream(jsonStream);
                })
                .UseNServiceBus(c =>
                {
                    var configuration = new EndpointConfiguration("whatever");
                    configuration.UseTransport<LearningTransport>();

                    var platformConfiguration = new ServicePlatformConnectionConfiguration();
                    c.Configuration.Bind("ServicePlatformConfiguration", platformConfiguration);

                    var beforeSettings = GetExplicitSettings(configuration);

                    configuration.ConnectToServicePlatform(platformConfiguration);

                    var afterSettings = GetExplicitSettings(configuration);

                    settingChanges = afterSettings.Except(beforeSettings)
                        .OrderBy(x => x)
                        .ToArray();

                    return configuration;
                });

            builder.Build();

            Approver.Verify(settingChanges);
        }

        static string[] GetExplicitSettings(EndpointConfiguration endpointConfig)
        {
            var settings = endpointConfig.GetSettings();

            var property = typeof(SettingsHolder).GetField("Overrides", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, "Overrides property cannot be found");
            var overrides = property.GetValue(settings) as ConcurrentDictionary<string, object>;
            Assert.IsNotNull(overrides);

            return overrides.Keys.ToArray();
        }
    }
}
