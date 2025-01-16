// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    public interface IDeviceTwin
    {
        string DeviceId { get; }

        string ETag { get; }

        ITwinPropertiesContainer Properties { get; }
        string GetTag(string tag);
    }
}
