/** Simple Network-based Clock for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

`NetworkClock` retrieves non-accurate, but believable time from `HEAD` response over `HTTPS`.

How to Use
==========
```cs
// initiate once on startup.
m_clock = new("your-server-address",
              (message, cert, chain, errors) => { ...verify certificate... },
              timeZoneOffset: TimeSpan.FromHours(9));

var currentTime = m_clock.Now;  // or .UtcNow
```

 */

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Believable (not accurate) date and time taken from <c>HEAD</c> response over <c>HTTPS</c>.
    /// </summary>
    public sealed class NetworkClock
    {
        sealed class NetworkClockException : Exception
        {
            NetworkClockException(string message, Exception? inner = null) : base(message, inner) { }

            [DoesNotReturn]
            public static void Throw(string message, Exception? inner = null) => throw new NetworkClockException(message, inner);
        }


        internal static async Task<DateTimeOffset> GetDateTimeFromHttpsResponseAsync(
            string address,
            RemoteCertificateValidationCallback certValidator,
            int retryCount,
            CancellationToken cancellationToken = default
            )
        {
            DateTimeOffset result = default;

            do
            {
                var connectionStartAt = Stopwatch.GetTimestamp();

                string host = address;
                using var client = new TcpClient(host, SslConnectionPort);

                using var ssl = new SslStream(client.GetStream(), false, certValidator, null, EncryptionPolicy.RequireEncryption);

                await ssl.AuthenticateAsClientAsync(host);

                var cert = ssl.RemoteCertificate as X509Certificate2;
                if (cert == null)
                {
                    NetworkClockException.Throw("certificate cannot be retrieved: " + host);
                }

                // get https response
                var requestBytes = Encoding.ASCII.GetBytes($"HEAD / HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n");
                await ssl.WriteAsync(requestBytes.AsMemory(), cancellationToken);

                using var reader = new StreamReader(ssl, Encoding.ASCII);

                await ssl.FlushAsync(cancellationToken);
                var response = await reader.ReadToEndAsync();

                const string DATE_HEADER = "\nDate: ";

                var dateHeaderStart = response.AsSpan().IndexOf(DATE_HEADER, StringComparison.Ordinal);
                if (dateHeaderStart < 0)
                {
                    NetworkClockException.Throw("failed to retrieve Date header: " + response);
                }

                dateHeaderStart += DATE_HEADER.Length;

                var dateHeaderEnd = response.AsSpan(dateHeaderStart).IndexOfAny('\r', '\n');
                if (dateHeaderEnd < 0)
                {
                    NetworkClockException.Throw("failed to find Date header end: " + response.Substring(dateHeaderStart));
                }

                dateHeaderEnd += dateHeaderStart;

                int dateHeaderLength = dateHeaderEnd - dateHeaderStart;
                if (!DateTimeOffset.TryParse(response.AsSpan(dateHeaderStart, dateHeaderLength), out result))
                {
                    NetworkClockException.Throw("failed to parse date and time from header: " + response.Substring(dateHeaderStart, dateHeaderLength));
                }

                if (GetElapsedTime(connectionStartAt) > ResponseDelayThreshold)
                {
                    checked
                    {
                        retryCount--;
                    }
                    continue;
                }

                break;
            }
            while (retryCount > 0);

            if (result == default)
            {
                NetworkClockException.Throw("failed to retrieve date from https HEAD request");
            }

            return result;
        }


        /*  create from scratch  ================================================================ */

        public static async Task<NetworkClock> CreateAsync(
            string address,
            TimeSpan timeZoneOffset,
            RemoteCertificateValidationCallback certValidator,
            int retryCount = 3,
            CancellationToken cancellationToken = default
            )
        {
            var origin = await GetDateTimeFromHttpsResponseAsync(address, certValidator, retryCount, cancellationToken);
            return new(origin.ToOffset(timeZoneOffset));
        }


        public static NetworkClock Create(
            string address,
            TimeSpan timeZoneOffset,
            RemoteCertificateValidationCallback certValidator,
            int retryCount = 3
            )
        {
            return Task.Run(async () =>
            {
                await Task.Delay(1).ConfigureAwait(false);  // make sure moving to thread pool.
                return await CreateAsync(address, timeZoneOffset, certValidator, retryCount);
            })
            .Result;
        }


        /*  public  ================================================================ */

        readonly static double TimestampToTicks = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        public static TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp = -1)
        {
            if (endTimestamp <= 0)
                endTimestamp = Stopwatch.GetTimestamp();

            return TimeSpan.FromTicks((long)((endTimestamp - startTimestamp) * TimestampToTicks));
        }


        public static TimeSpan ResponseDelayThreshold { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = TimeSpan.FromSeconds(7);

        public static int SslConnectionPort { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = 443;


        /// <param name="timestamp"><c>-1</c> to use current timestamp.</param>
        public static NetworkClock Create(DateTimeOffset origin, TimeSpan timeZoneOffset, long timestamp = -1)
        {
            if (timestamp <= 0)
                timestamp = Stopwatch.GetTimestamp();

            return new(origin.ToOffset(timeZoneOffset), timestamp);
        }

        /// <inheritdoc cref="Create(DateTimeOffset, TimeSpan, long)"/>
        public static NetworkClock Create(HttpResponseMessage response, TimeSpan timeZoneOffset, long timestamp = -1)
        {
            var origin = response.Headers.Date;
            if (!origin.HasValue)
            {
                NetworkClockException.Throw("response doesn't have Date header");
            }

            return Create(origin.Value, timeZoneOffset, timestamp);
        }


#pragma warning disable IDE0079
#pragma warning disable CA1822   // Instance/Default/Shared members should not be made static
#pragma warning restore IDE0079

        readonly DateTimeOffset origin;
        readonly long timestamp;

        NetworkClock(DateTimeOffset origin)
        {
            this.origin = origin;
            this.timestamp = Stopwatch.GetTimestamp();
        }

        NetworkClock(DateTimeOffset origin, long timestamp)
        {
            this.origin = origin;
            this.timestamp = timestamp;
        }


        public DateTimeOffset Now => origin.Add(GetElapsedTime(this.timestamp));
        public DateTimeOffset UtcNow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Now.ToOffset(TimeSpan.Zero);
        }

        public override string ToString() => Now.ToString();
        public string ToString(string format, IFormatProvider? provider = null) => Now.ToString(format, provider);


#if UNITY_EDITOR
        [UnityEditor.MenuItem("TEST/" + nameof(NetworkClock))]
        static void TEST()
        {
            var clock = Create(
                "1.1.1.1",
                TimeSpan.FromHours(9),
                static (message, cert, chain, errors) =>
                {
                    X509Certificate2? rootCert = null;

                    foreach (var elem in chain.ChainElements)
                    {
                        rootCert = elem.Certificate;
                    }

                    if (rootCert != null)
                    {
                        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                        store.Open(OpenFlags.ReadOnly);

                        foreach (var r in store.Certificates)
                        {
                            if (r.Thumbprint == rootCert.Thumbprint)
                            {
                                return errors == SslPolicyErrors.None;
                            }
                        }
                    }

                    return false;
                });

            _ = Task.Run(() =>
            {
                UnityEngine.Debug.Log(clock.UtcNow + " (utc)");
                UnityEngine.Debug.Log(clock);

                for (int i = 0; i < 3; i++)
                {
                    Thread.Sleep(1000);
                    UnityEngine.Debug.Log(clock.Now.ToString());
                }
            });
        }
#endif

    }
}
