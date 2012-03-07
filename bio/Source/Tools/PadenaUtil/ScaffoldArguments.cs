﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bio;
using Bio.Algorithms.Assembly.Padena.Scaffold;
using Bio.IO.FastA;
using PadenaUtil.Properties;

namespace PadenaUtil
{
    /// <summary>
    /// This class defines Scaffolding Option.
    /// </summary>
    internal class ScaffoldArguments
    {
        #region Public Fields

        /// <summary>
        /// Length of k-mer.
        /// </summary>
        public int KmerLength = -1;

        /// <summary>
        /// Help.
        /// </summary>
        public bool Help = false;

        /// <summary>
        /// Input file of reads and contigs.
        /// </summary>
        public string[] FileNames = null;

        /// <summary>
        /// Output file.
        /// </summary>
        public string OutputFile = string.Empty;

        /// <summary>
        /// Clone Library Name
        /// </summary>
        public string CloneLibraryName = string.Empty;

        /// <summary>
        /// Mean Length of clone library.
        /// </summary>
        public double MeanLengthOfInsert = 0;

        /// <summary>
        /// Standard Deviation of Clone Library.
        /// </summary>
        public double StandardDeviationOfInsert = 0;

        /// <summary>
        /// Number of paired read required to connect two contigs.
        /// </summary>
        public int Redundancy = 2;

        /// <summary>
        /// Depth for graph traversal.
        /// </summary>
        public int Depth = 10;

        /// <summary>
        /// Display verbose logging during processing.
        /// </summary>
        public bool Verbose = false;

        /// <summary>
        /// Quiet flag (no logging)
        /// </summary>
        public bool Quiet = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// Generates the Scaffold.
        /// </summary>
        public void GenerateScaffold()
        {
            Output.WriteLine(OutputLevel.Information, Resources.ScaffoldStarting);

            if (this.FileNames.Length != 2)
            {
                Output.WriteLine(OutputLevel.Error, "\nError: A reference file and 1 query file are required.");
                Output.WriteLine(OutputLevel.Required, Resources.ScaffoldHelp);
                return;
            }

            if (!string.IsNullOrEmpty(this.CloneLibraryName))
            {
                CloneLibrary.Instance.AddLibrary(this.CloneLibraryName, (float)this.MeanLengthOfInsert, (float)this.StandardDeviationOfInsert);
            }

            TimeSpan algorithmSpan = new TimeSpan();
            Stopwatch runAlgorithm = new Stopwatch();
            FileInfo refFileinfo = null;

            using (GraphScaffoldBuilder scaffoldBuilder = new GraphScaffoldBuilder())
            {
                refFileinfo = new FileInfo(this.FileNames[0]);
                long refFileLength = refFileinfo.Length;

                runAlgorithm.Restart();
                IEnumerable<ISequence> contigs = AssembleArguments.ParseFile(this.FileNames[0]);
                runAlgorithm.Stop();
                algorithmSpan = algorithmSpan.Add(runAlgorithm.Elapsed);

                if (this.Verbose)
                {
                    Output.WriteLine(OutputLevel.Verbose);
                    Output.WriteLine(OutputLevel.Verbose, "Processed contigs file : {0}", Path.GetFullPath(this.FileNames[0]));
                    Output.WriteLine(OutputLevel.Verbose, "   Read/Processing time: {0}", runAlgorithm.Elapsed);
                    Output.WriteLine(OutputLevel.Verbose, "   File Size           : {0}", refFileLength);
                }

                refFileinfo = new FileInfo(this.FileNames[1]);
                refFileLength = refFileinfo.Length;

                runAlgorithm.Restart();
                IEnumerable<ISequence> reads = AssembleArguments.ParseFile(this.FileNames[1]);
                runAlgorithm.Stop();
                algorithmSpan = algorithmSpan.Add(runAlgorithm.Elapsed);

                if (this.Verbose)
                {
                    Output.WriteLine(OutputLevel.Verbose);
                    Output.WriteLine(OutputLevel.Verbose, "Processed reads file   : {0}", Path.GetFullPath(this.FileNames[1]));
                    Output.WriteLine(OutputLevel.Verbose, "   Read/Processing time: {0}", runAlgorithm.Elapsed);
                    Output.WriteLine(OutputLevel.Verbose, "   File Size           : {0}", refFileLength);
                }

                runAlgorithm.Restart();
                ValidateAmbiguousReads(reads);
                runAlgorithm.Stop();

                if (this.Verbose)
                {
                    Output.WriteLine(OutputLevel.Verbose);
                    Output.WriteLine(OutputLevel.Verbose, "Time taken for Validating reads: {0}", runAlgorithm.Elapsed);
                }

                runAlgorithm.Restart();
                IList<ISequence> scaffolds = scaffoldBuilder.BuildScaffold(reads, contigs.ToList(), this.KmerLength, this.Depth, this.Redundancy);
                runAlgorithm.Stop();
                algorithmSpan = algorithmSpan.Add(runAlgorithm.Elapsed);
                if (this.Verbose)
                {
                    Output.WriteLine(OutputLevel.Verbose);
                    Output.WriteLine(OutputLevel.Verbose, "Compute time: {0}", runAlgorithm.Elapsed);
                }

                runAlgorithm.Restart();
                WriteContigs(scaffolds);
                runAlgorithm.Stop();
                algorithmSpan = algorithmSpan.Add(runAlgorithm.Elapsed);
                if (this.Verbose)
                {
                    Output.WriteLine(OutputLevel.Verbose);
                    Output.WriteLine(OutputLevel.Verbose, "Write time: {0}", runAlgorithm.Elapsed);
                    Output.WriteLine(OutputLevel.Verbose, "Total runtime: {0}", algorithmSpan);
                }
            }
        }

        #endregion

        #region Private Members

        /// <summary>
        /// It writes Contigs to the file.
        /// </summary>
        /// <param name="scaffolds">The list of scaffolds sequence.</param>
        private void WriteContigs(IList<ISequence> scaffolds)
        {
            if (scaffolds.Count == 0)
            {
                Output.WriteLine(OutputLevel.Information, "No Scaffolds generated.");
                return;
            }

            EnsureContigNames(scaffolds);

            if (!string.IsNullOrEmpty(this.OutputFile))
            {
                using (FastAFormatter formatter = new FastAFormatter(this.OutputFile))
                {
                    formatter.AutoFlush = true;

                    foreach (ISequence seq in scaffolds)
                    {
                        formatter.Write(seq);
                    }
                }
                Output.WriteLine(OutputLevel.Information, "Wrote {0} scaffolds to {1}", scaffolds.Count, this.OutputFile);
            }
            else
            {
                Output.WriteLine(OutputLevel.Information, "Scaffolds Results:");
                foreach (ISequence seq in scaffolds)
                {
                    Output.WriteLine(OutputLevel.Results, seq.ID);
                    Output.WriteLine(OutputLevel.Results, new string(seq.Select(a => (char)a).ToArray()));
                }
            }
        }

        /// <summary>
        /// Ensures the sequence contigs have a valid ID. If no ID is present
        /// then one is generated from the index and filename.
        /// </summary>
        /// <param name="sequences"></param>
        private void EnsureContigNames(IList<ISequence> sequences)
        {
            string filename = Path.GetFileNameWithoutExtension(this.FileNames[1]) + "_{0}";
            for (int index = 0; index < sequences.Count; index++)
            {
                ISequence inputSequence = sequences[index];
                if (string.IsNullOrEmpty(inputSequence.ID))
                    inputSequence.ID = string.Format(filename, index + 1);
            }
        }

        /// <summary>
        /// Checks for ambiguous reads if any, if found ArgumentException.
        /// </summary>
        /// <param name="reads">Input reads.</param>
        private static void ValidateAmbiguousReads(IEnumerable<ISequence> reads)
        {
            if (reads.Any(s => s.Alphabet.HasAmbiguity))
                throw new ArgumentException(Properties.Resources.AmbiguousReadsNotSupported);
        }
        #endregion
    }
}
