﻿using System;
using System.IO;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoRawFileParser.Writer;

namespace ThermoRawFileParser
{
    public class RawFileParser
    {
        private static readonly log4net.ILog Log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Extract the RAW file metadata and spectra in MGF format. 
        /// </summary>
        /// <param name="parseInput">the parse input object</param>
        public static void Parse(ParseInput parseInput)
        {
            // Check to see if the RAW file name was supplied as an argument to the program
            if (string.IsNullOrEmpty(parseInput.RawFilePath))
            {
                Log.Error("No RAW file specified!");

                return;
            }

            // Check to see if the specified RAW file exists
            if (!File.Exists(parseInput.RawFilePath))
            {
                Log.Error(@"The file doesn't exist in the specified location - " + parseInput.RawFilePath);

                return;
            }

            Log.Info("Started parsing " + parseInput.RawFilePath);

            // Create the IRawDataPlus object for accessing the RAW file
            //var rawFile = RawFileReaderAdapter.FileFactory(rawFilePath);
            IRawDataPlus rawFile;
            using (rawFile = RawFileReaderFactory.ReadFile(parseInput.RawFilePath))
            {
                if (!rawFile.IsOpen)
                {
                    Log.Error("Unable to access the RAW file using the RawFileReader class!");

                    return;
                }

                // Check for any errors in the RAW file
                if (rawFile.IsError)
                {
                    Log.Error($"Error opening ({rawFile.FileError}) - {parseInput.RawFilePath}");

                    return;
                }

                // Check if the RAW file is being acquired
                if (rawFile.InAcquisition)
                {
                    Log.Error("RAW file still being acquired - " + parseInput.RawFilePath);

                    return;
                }

                // Get the number of instruments (controllers) present in the RAW file and set the 
                // selected instrument to the MS instrument, first instance of it
                rawFile.SelectInstrument(Device.MS, 1);

                // Get the first and last scan from the RAW file
                var firstScanNumber = rawFile.RunHeaderEx.FirstSpectrum;
                var lastScanNumber = rawFile.RunHeaderEx.LastSpectrum;

                if (parseInput.OutputMetadata != MetadataFormat.NON)
                {
                    var metadataWriter = new MetadataWriter(parseInput.OutputDirectory, parseInput.RawFileNameWithoutExtension);
                    if(parseInput.OutputMetadata == MetadataFormat.JSON)
                        metadataWriter.WriteJsonMetada(rawFile, firstScanNumber, lastScanNumber);
                    if(parseInput.OutputMetadata == MetadataFormat.TXT)
                        metadataWriter.WriteMetada(rawFile, firstScanNumber, lastScanNumber);
                    
                }

                SpectrumWriter spectrumWriter;
                if (parseInput.OutputFormat != OutputFormat.NON)
                {
                    switch (parseInput.OutputFormat)
                    {
                        case OutputFormat.Mgf:
                            spectrumWriter = new MgfSpectrumWriter(parseInput);
                            spectrumWriter.Write(rawFile, firstScanNumber, lastScanNumber);
                            break;
                        case OutputFormat.Mzml:
                            spectrumWriter = new MzMlSpectrumWriter(parseInput);
                            spectrumWriter.Write(rawFile, firstScanNumber, lastScanNumber);
                            break;
                      }
                    
                }

                Log.Info("Finished parsing " + parseInput.RawFilePath);
            }
        }
    }
}