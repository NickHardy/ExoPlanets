#region "copyright"

/*
    Copyright © 2016 - 2022 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility.Rules {

    public class ListOfIntegers : ValidationRule {

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            try {
                _ = value.ToString().Split(',').Select(int.Parse).ToList();
            } catch (Exception) {
                return new ValidationResult(false, "Must be a comma separated list of integers");
            }

            return new ValidationResult(true, null);
        }
    }

    public class FileExist : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return new ValidationResult(true, null);
        }
    }

}