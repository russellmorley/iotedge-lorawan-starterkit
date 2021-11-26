// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    /// <summary>
    /// The 32-bit ephemeral device address assigned to a device when it joins a network. It is
    /// assigned during the join process for devices that join a network using the (preferred)
    /// over-the-air activation (OTAA) technique.
    /// </summary>
    public readonly record struct DevAddr
    {
        /* +--------|---------+
           | 31..25 |  24..0  |
           +--------+---------+
           |  NwkID | NwkAddr |
           +--------|---------+ */

        public const int Size = sizeof(uint);

        private const uint NetworkAddressMask = 0x01ff_ffff;
        private readonly uint value;

        public DevAddr(uint value) => this.value = value;

        /// <summary>
        /// The <c>NwkID</c> (bits 25..31).
        /// </summary>
        public int NetworkId => unchecked((int)((this.value & ~NetworkAddressMask) >> 25));

        /// <summary>
        /// The <c>NwkAddr</c> (bits 0..24).
        /// </summary>
        public int NetworkAddress => unchecked((int)(this.value & NetworkAddressMask));

        public override string ToString() => this.value.ToString("X8", CultureInfo.InvariantCulture);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, this.value);
            return buffer[Size..];
        }
    }
}