using System;
using System.Collections.Generic;
using System.IO;
using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.Interfaces;

namespace ThermoRawFileParser.XIC
{
    public class XicReader
    {
        private const string MsFilter = "ms";

        public static void ReadXic(string rawFilePath, bool base64, XicData xicData)
        {
            IRawDataPlus rawFile;
            using (rawFile = RawFileReaderFactory.ReadFile(rawFilePath))
            {
                if (!rawFile.IsOpen)
                {
                    throw new RawFileParserException("Unable to access the RAW file using the native Thermo library.");
                }

                // Check for any errors in the RAW file
                if (rawFile.IsError)
                {
                    throw new RawFileParserException(
                        $"Error opening ({rawFile.FileError}) - {rawFilePath}");
                }

                // Check if the RAW file is being acquired
                if (rawFile.InAcquisition)
                {
                    throw new RawFileParserException("RAW file still being acquired - " + rawFilePath);
                }

                // Get the number of instruments (controllers) present in the RAW file and set the 
                // selected instrument to the MS instrument, first instance of it
                rawFile.SelectInstrument(Device.MS, 1);

                // Get the first and last scan from the RAW file
                var firstScanNumber = rawFile.RunHeaderEx.FirstSpectrum;
                var lastScanNumber = rawFile.RunHeaderEx.LastSpectrum;

                // Get the start and end time from the RAW file
                var startTime = rawFile.RunHeaderEx.StartTime;
                var endTime = rawFile.RunHeaderEx.EndTime;

                // Get the mass range from the RAW file
                var minMass = rawFile.RunHeaderEx.LowMass;
                var maxMass = rawFile.RunHeaderEx.HighMass;

                // Update global metadata
                xicData.OutputMeta.base64 = base64;
                xicData.OutputMeta.timeunit = "minutes";

                foreach (var xicUnit in xicData.Content)
                {
                    IChromatogramSettings settings = null;
                    if (!xicUnit.Meta.MzStart.HasValue && !xicUnit.Meta.MzEnd.HasValue)
                    {
                        settings = new ChromatogramTraceSettings()
                        {
                            Filter = xicUnit.Meta.Filter ?? "ms"
                        };
                    }

                    if (!xicUnit.Meta.MzStart.HasValue)
                    {
                        xicUnit.Meta.MzStart = minMass;
                    }

                    if (!xicUnit.Meta.MzEnd.HasValue)
                    {
                        xicUnit.Meta.MzEnd = maxMass;
                    }

                    if (settings == null)
                    {
                        settings = new ChromatogramTraceSettings(TraceType.MassRange)
                        {
                            Filter = xicUnit.Meta.Filter ?? "ms",
                            MassRanges = new[]
                            {
                                new Range(xicUnit.Meta.MzStart.Value,
                                    xicUnit.Meta.MzEnd.Value)
                            }
                        };
                    }

                    List<int> rtFilteredScans = null;
                    if (!xicUnit.Meta.RtStart.HasValue && !xicUnit.Meta.RtEnd.HasValue)
                    {
                        rtFilteredScans = new List<int>();
                    }

                    if (!xicUnit.Meta.RtStart.HasValue)
                    {
                        xicUnit.Meta.RtStart = startTime;
                    }

                    if (!xicUnit.Meta.RtEnd.HasValue)
                    {
                        xicUnit.Meta.RtEnd = endTime;
                    }

                    if (rtFilteredScans == null)
                    {
                        rtFilteredScans = rawFile.GetFilteredScansListByTimeRange(MsFilter,
                            xicUnit.Meta.RtStart.Value, xicUnit.Meta.RtEnd.Value);
                    }

                    IChromatogramData data;
                    if (!rtFilteredScans.IsNullOrEmpty())
                    {
                        data = rawFile.GetChromatogramData(new IChromatogramSettings[] {settings}, rtFilteredScans[0],
                            rtFilteredScans[rtFilteredScans.Count - 1]);
                    }
                    else
                    {
                        data = rawFile.GetChromatogramData(new IChromatogramSettings[] {settings}, firstScanNumber,
                            lastScanNumber);
                    }

                    var chromatogramTrace = ChromatogramSignal.FromChromatogramData(data);
                    if (chromatogramTrace[0].Scans.Count != 0)
                    {
                        if (!base64)
                        {
                            xicUnit.RetentionTimes = chromatogramTrace[0].Times;
                            xicUnit.Intensities = chromatogramTrace[0].Intensities;
                        }
                        else
                        {
                            xicUnit.RetentionTimes = GetBase64String(chromatogramTrace[0].Times);
                            xicUnit.Intensities = GetBase64String(chromatogramTrace[0].Intensities);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Convert the double array into a base64 string
        /// </summary>
        /// <param name="array">the double collection</param>
        /// <returns>the base64 string</returns>
        private static string GetBase64String(IEnumerable<double> array)
        {
            byte[] bytes;

            using (var memoryStream = new MemoryStream())
            {
                foreach (var doubleValue in array)
                {
                    var doubleValueByteArray = BitConverter.GetBytes(doubleValue);
                    memoryStream.Write(doubleValueByteArray, 0, doubleValueByteArray.Length);
                }

                memoryStream.Position = 0;
                bytes = memoryStream.ToArray();
            }

            return Convert.ToBase64String(bytes);
        }
    }
}