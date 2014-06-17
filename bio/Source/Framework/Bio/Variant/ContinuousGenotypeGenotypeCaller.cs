﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bio.Variant
{
	/// <summary>
	/// Class takes a list of read pileups and emits at locations without deletions, the posterior probability of each base. 
	/// The model is as follows.
	/// 
	/// Each position is drawn from a vector (A, C, G, T, -) 
	/// 
	/// The error model is as follows, for each 
	/// </summary>
	public class ContinuousGenotypeCaller
	{
		/// <summary>
		/// If a column has above this percentage of deletions, than this base
		/// and neighboring bases are not called.
		/// </summary>
		const double MIN_DELETION_PERCENTAGE_NEEDED_TO_CALL = 0.1;

		/// <summary>
		/// The value used to determine if the EM algorithm terminates. When the change in likelihood is 
		/// less than this amount, nothing is altered.
		/// </summary>
		const double EM_TERMINATION_CONDITION = 1e-3;

		/// <summary>
		/// Should we use the EM algorithm or just read counts?
		/// </summary>
		const bool DO_EM_ESTIMATION = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="Bio.Variant.ContinuousGenotypeCaller"/> class.
		/// </summary>
		/// <param name="sortedPileUps">Sorted pile ups.</param>
		public ContinuousGenotypeCaller (IEnumerable<PileUp> sortedPileUps)
		{


		}
		private ContinuousFrequencyGenotype callGenotype(PileUp pu)
		{
			//If it looks like a deletion, skip it
			if(pileupHasTooManyIndels(pu)) {
				return new ContinuousFrequencyGenotype (SimpleGenotypeCallResult.NoCall_TooManyDeletions);
			}

			// Otherwise, drop gaps, ambiguous bases and low scoring reads.
			var filteredBases = pu.Bases.Where (z => z.Base != BaseAndQuality.N_BASE_INDEX &&
			                    z.Base != BaseAndQuality.GAP_BASE_INDEX && z.PhredScore > 17).ToArray ();

			// initialize the continuous frequency based on counts of bases.
			var base_pair_counts = new int[BasePairFrequencies.NUM_BASES];
			foreach (var bp in filteredBases) {
				base_pair_counts [bp.Base]++;
			}
			var freqs = base_pair_counts.Select (x => x / pu.Bases.Count).ToArray ();
			var theta = new BasePairFrequencies ( freqs );

			//Do an EM optimization of just counts? 
			if (DO_EM_ESTIMATION) {
				//first make an NumReads * Num_Bases Matrix
				double[][] conditionalProbs = new double[filteredBases.Count][];
				for (int i = 0; i < filteredBases.Count; i++) {
					conditionalProbs [i] = new double[BasePairFrequencies.NUM_BASES];
				}

				double likDif = double.MinValue;
				double last_lik = double.MinValue;
				while (likDif > 1e-3) {
					double lik = 0;
					for (int i = 0; i < conditionalProbs.Length; i++) {
						lik += updateGonditionalProbabilities (theta, conditionalProbs [i], filteredBases [i]);
					}
					likDif = lik - last_lik;
					last_lik = lik;

					//now update thetas by summing the conditional probability of each value
					Array.Clear (theta.Frequencies);
					for (int i = 0; i < conditionalProbs.Length; i++) {
						var cur = conditionalProbs [i];
						for (int j = 0; j < cur.Length; j++) {
							theta.Frequencies [j] += cur [j];
						}
					}
					for (int j = 0; j < theta.Frequencies.Length; j++) {
						theta.Frequencies [j] /= (double)conditionalProbs.Length;
					}
				}
			}
		}

		/// <summary>
		/// Calculates the conditional probability that a base comes from each of A, C, G, T 
		/// based on the observed read and quality and current parameter settings.
		/// </summary>
		/// <returns>The gonditional probabilities.</returns>
		/// <param name="freqs">Freqs.</param>
		/// <param name="data">Data.</param>
		/// <param name="bp">Bp.</param>
		private double updateGonditionalProbabilities(BasePairFrequencies freqs, double[] data, BaseAndQuality bp ) {
			//Calculate conditional probability that the base comes from the observed base.

			double probRight = BaseQualityUtils.GetCorrectProbability (bp.PhredScore);
			double probWrong = .333333333 * BaseQualityUtils.GetErrorProbability(bp.PhredScore); 
			double totProb = 0.0;
			for (int i = 0; i < data.Length; i++) {
					double  prob = freqs.Frequencies [i] * (i == bp.Base ? probRight : probWrong); 
					totProb += prob;
					data[i] = prob;
			}
			for (int i = 0; i < data.Length; i++) {
				data[i] /= totProb;
			}
			return Math.Log(totProb);
		}

		private bool pileupHasTooManyIndels(PileUp up)
		{
			var freq = up.Bases.Count (z => z == BaseAndQuality.GAP_BASE_INDEX) / (double)up.Bases.Count;
			return freq >= MIN_DELETION_PERCENTAGE_NEEDED_TO_CALL;
		}
	}
}
