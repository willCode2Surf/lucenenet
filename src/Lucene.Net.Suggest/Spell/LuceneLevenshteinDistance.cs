﻿using System;
using Lucene.Net.Support;
using Lucene.Net.Util;

namespace Lucene.Net.Search.Spell
{

    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    ///  Damerau-Levenshtein (optimal string alignment) implemented in a consistent 
    ///  way as Lucene's FuzzyTermsEnum with the transpositions option enabled.
    ///  
    ///  Notes:
    ///  <ul>
    ///    <li> This metric treats full unicode codepoints as characters
    ///    <li> This metric scales raw edit distances into a floating point score
    ///         based upon the shortest of the two terms
    ///    <li> Transpositions of two adjacent codepoints are treated as primitive 
    ///         edits.
    ///    <li> Edits are applied in parallel: for example, "ab" and "bca" have 
    ///         distance 3.
    ///  </ul>
    ///  
    ///  NOTE: this class is not particularly efficient. It is only intended
    ///  for merging results from multiple DirectSpellCheckers.
    /// </summary>
    public sealed class LuceneLevenshteinDistance : StringDistance
    {

        /// <summary>
        /// Creates a new comparator, mimicing the behavior of Lucene's internal
        /// edit distance.
        /// </summary>
        public LuceneLevenshteinDistance()
        {
        }

        public float GetDistance(string target, string other)
        {
            IntsRef targetPoints;
            IntsRef otherPoints;
            int n;
            int[][] d; // cost array

            // NOTE: if we cared, we could 3*m space instead of m*n space, similar to 
            // what LevenshteinDistance does, except cycling thru a ring of three 
            // horizontal cost arrays... but this comparator is never actually used by 
            // DirectSpellChecker, its only used for merging results from multiple shards 
            // in "distributed spellcheck", and its inefficient in other ways too...

            // cheaper to do this up front once
            targetPoints = ToIntsRef(target);
            otherPoints = ToIntsRef(other);
            n = targetPoints.Length;
            int m = otherPoints.Length;

            //TODO The following call to the 'RectangularArrays' helper class reproduces the rectangular array initialization that is automatic in Java: (ORIGINAL LINE: d = new int[n+1][m+1];)
            d = RectangularArrays.ReturnRectangularIntArray(n + 1, m + 1);

            if (n == 0 || m == 0)
            {
                if (n == m)
                {
                    return 0;
                }
                else
                {
                    return Math.Max(n, m);
                }
            }

            // indexes into strings s and t
            int i; // iterates through s
            int j; // iterates through t

            int t_j; // jth character of t

            int cost; // cost

            for (i = 0; i <= n; i++)
            {
                d[i][0] = i;
            }

            for (j = 0; j <= m; j++)
            {
                d[0][j] = j;
            }

            for (j = 1; j <= m; j++)
            {
                t_j = otherPoints.Ints[j - 1];

                for (i = 1; i <= n; i++)
                {
                    cost = targetPoints.Ints[i - 1] == t_j ? 0 : 1;
                    // minimum of cell to the left+1, to the top+1, diagonally left and up +cost
                    d[i][j] = Math.Min(Math.Min(d[i - 1][j] + 1, d[i][j - 1] + 1), d[i - 1][j - 1] + cost);
                    // transposition
                    if (i > 1 && j > 1 && targetPoints.Ints[i - 1] == otherPoints.Ints[j - 2] && targetPoints.Ints[i - 2] == otherPoints.Ints[j - 1])
                    {
                        d[i][j] = Math.Min(d[i][j], d[i - 2][j - 2] + cost);
                    }
                }
            }

            return 1.0f - ((float)d[n][m] / Math.Min(m, n));
        }

        private static IntsRef ToIntsRef(string s)
        {
            var @ref = new IntsRef(s.Length); // worst case
            int utf16Len = s.Length;
            for (int i = 0, cp = 0; i < utf16Len; i += Character.CharCount(cp))
            {
                cp = @ref.Ints[@ref.Length++] = Character.CodePointAt(s, i);
            }
            return @ref;
        }
    }
}