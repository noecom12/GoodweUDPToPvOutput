﻿using System;
using System.IO;
using System.Text.Json.Serialization;

namespace GoodweUdpPoller
{
    public class InverterTelemetry
    {
        public static InverterTelemetry Create(ReadOnlySpan<byte> data)
        {
            var expectedLength = 153;
            if (data.Length != expectedLength)
                throw new InvalidDataException($"Got size {data.Length}, expected {expectedLength}");
            var header = data.Slice(0, 2);
            if (!header.SequenceEqual(new byte[] { 0xaa, 0x55 }))
            {
                throw new InvalidDataException($"Wrong header");
            }

            var receivedCrc = data.Slice(data.Length - 2);
            var payload = data.Slice(2, data.Length - 4);
            if (!receivedCrc.SequenceEqual(GoodweCrc(payload)))
            {
                throw new InvalidDataException($"CRC mismatch");
            }

            return new InverterTelemetry
            {
                Year = data[5] + 2000,
                Month = data[6],
                Day = data[7],
                Hour = data[8],
                Minute = data[9],
                Second = data[10],
                Vpv = data.To16BitScale10(11),
                Ipv = data.To16BitScale10(13),
                Vac = data.To16BitScale10(41),
                Iac = data.To16BitScale10(47),
                GridFrequency = data.To16BitScale100(53),
                Power = data.To16Bit(61),
                Status = (InverterStatus)data[63],
                Temperature = data.To16BitScale10(87),
                EnergyToday = data.To16BitScale10(93),
                EnergyLifetime = data.To16BitScale10(97)/*probably 32 bit*/,
            };
        }

        /// <summary>
        /// Temperature in degrees Celsius
        /// </summary>
        public double Temperature { get; set; }

        public InverterStatus Status { get; set; }

        public double EnergyLifetime { get; set; }

        public double EnergyToday { get; set; }

        /// <summary>
        /// Momentary power at timestamp, in W
        /// </summary>
        public double Power { get; set; }

        public double Iac { get; set; }

        public double Vac { get; set; }

        public double GridFrequency { get; set; }

        /// <summary>
        /// DC Current from the solar array, in A 
        /// </summary>
        public double Ipv { get; set; }

        /// <summary>
        /// DC Voltage from the solar array, in V
        /// </summary>
        public double Vpv { get; set; }

        /// <summary>
        /// Timestamp of the telemetry according to the inverter, second precision.
        /// </summary>
        public DateTimeOffset Timestamp => new DateTimeOffset(Year, Month, Day, Hour, Minute, Second, 0, TimeSpan.Zero);
        private byte Second { get; set; }

        private byte Minute { get; set; }

        private byte Hour { get; set; }

        private byte Day { get; set; }

        private byte Month { get; set; }

        private int Year { get; set; }

        public static byte[] GoodweCrc(ReadOnlySpan<byte> payload)
        {
            var crc = 0xFFFF;
            bool odd;

            for (var i = 0; i < payload.Length; i++)
            {
                crc ^= payload[i];

                for (var j = 0; j < 8; j++)
                {
                    odd = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (odd)
                    {
                        crc ^= 0xA001;
                    }
                }
            }
            return BitConverter.GetBytes((ushort)crc);
        }

        public enum InverterStatus
        {
            Waiting,
            Normal,
            Error,
            Checking
        }
    }
}
