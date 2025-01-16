// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.FunctionBundler
{
    public class DeduplicationResult
    {
        public bool IsDuplicate { get; set; }

        public string GatewayId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we can process this message.
        /// 
        /// Only used on the network server
        /// </summary>
        public bool CanProcess { get; set; }
    }
}
