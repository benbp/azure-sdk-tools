using Azure.Data.AppConfiguration;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GlobalConfigurationProvider : IGlobalConfigurationProvider
    {
        private ConfigurationClient appConfig;

        public GlobalConfigurationProvider(ConfigurationClient appConfig)
        {
            this.appConfig = appConfig;
        }

        private object applicationIDLock = new object();
        private string applicationID;

        public string GetApplicationMode()
        {
            var mode = Environment.GetEnvironmentVariable("CHECK_ENFORCER_MODE")?.ToLower();
            if (string.IsNullOrEmpty(mode)) {
                return "external";
            }
            if (mode != "external" && mode != "local")
            {
                throw new CheckEnforcerConfigurationException($"Unsupported CHECK_ENFORCER_MODE '{mode}'");
            }
            return mode;
        }

        public string GetApplicationID()
        {
            if (appConfig == null)
            {
                return "local";
            }

            if (applicationID == null)
            {
                lock(applicationIDLock)
                {
                    if (applicationID == null)
                    {
                        ConfigurationSetting applicationIDSetting = appConfig.GetConfigurationSetting(
                            "checkenforcer/github-app-id"
                            );
                        applicationID = applicationIDSetting.Value;
                    }
                }
            }

            return applicationID;
        }

        private object applicationNameLock = new object();
        private string applicationName;

        public string GetApplicationName()
        {
            if (appConfig == null)
            {
                return "local";
            }

            if (applicationName == null)
            {
                lock (applicationNameLock)
                {
                    if (applicationName == null)
                    {
                        ConfigurationSetting applicationNameSetting = appConfig.GetConfigurationSetting(
                            "checkenforcer/check-name"
                            );
                        applicationName = applicationNameSetting.Value;
                    }
                }
            }

            return applicationName;
        }

        private object maxRequestsPerPeriodLock = new object();
        private int maxRequestsPerPeriod = -1;

        public int GetMaxRequestsPerPeriod()
        {
            if (appConfig == null)
            {
                return 1;
            }

            if (maxRequestsPerPeriod == -1)
            {
                lock (maxRequestsPerPeriodLock)
                {
                    if (maxRequestsPerPeriod == -1)
                    {
                        ConfigurationSetting applicationNameSetting = appConfig.GetConfigurationSetting(
                            "checkenforcer/max-requests-per-period"
                            );
                        maxRequestsPerPeriod = int.Parse(applicationNameSetting.Value);
                    }
                }
            }

            return maxRequestsPerPeriod;
        }

        private object periodDurationInSecondsLock = new object();
        private int periodDurationInSeconds = -1;

        public int GetPeriodDurationInSeconds()
        {
            if (appConfig == null)
            {
                return 1;
            }

            if (periodDurationInSeconds == -1)
            {
                lock (periodDurationInSecondsLock)
                {
                    if (periodDurationInSeconds == -1)
                    {
                        ConfigurationSetting applicationNameSetting = appConfig.GetConfigurationSetting(
                            "checkenforcer/period-duration-in-seconds"
                            );
                        periodDurationInSeconds = int.Parse(applicationNameSetting.Value);
                    }
                }
            }

            return periodDurationInSeconds;
        }

    }
}
