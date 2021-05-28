﻿using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    public class HeaderKeyValueSanitizer : RecordedTestSanitizer
    {
        private string _targetKey;
        private string[] _newValue;

        public HeaderKeyValueSanitizer(string key, string value)
        {
            _targetKey = key;
            _newValue = new string[] { value };
        }

        public override void SanitizeHeaders(IDictionary<string, string[]> headers)
        {
            if (headers.ContainsKey(_targetKey))
            {
                headers[_targetKey] = _newValue;
            }
        }

        // TODO: explore the body and do a key/value replace if necessary there as well
    }

}