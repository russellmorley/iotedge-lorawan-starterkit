// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
#pragma warning disable CA1028 // Enum Storage should be Int32
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
    // Not applicable in this case.
    public enum Fctrl : short
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
#pragma warning restore CA1028 // Enum Storage should be Int32
    {
        FOptLen1 = 0,
        FOptLen2 = 1,
        FOptLen3 = 2,
        FOptLen4 = 4,
        FpendingOrClassB = 16,
        Ack = 32,
        ADRAckReq = 64,
        ADR = 128
    }
}