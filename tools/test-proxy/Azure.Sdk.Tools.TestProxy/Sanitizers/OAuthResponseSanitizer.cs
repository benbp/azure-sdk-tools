﻿using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    public class OAuthResponseSanitizer : RecordedTestSanitizer
    {
        public static Regex rx = new Regex("/oauth2(?:/v2.0)?/token");

        public override void Sanitize(RecordSession session)
        {
            session.Entries.RemoveAll(x => rx.IsMatch(x.RequestUri));
        }
    }
}