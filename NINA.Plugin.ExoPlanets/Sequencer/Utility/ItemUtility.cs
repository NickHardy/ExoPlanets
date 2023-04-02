#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Sequencer.Container;
using NINA.Astrometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NINA.Plugin.ExoPlanets.Sequencer.Container;
using NINA.Plugin.ExoPlanets.Model;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Equipment.Model;
using NINA.Plugin.ExoPlanets.Interfaces;

namespace NINA.Plugin.ExoPlanets.Sequencer.Utility {

    public class ItemUtility {

        public static ExoPlanetDeepSkyObject RetrieveExoPlanetDSO(ISequenceContainer parent) {
            if (parent != null) {
                var container = parent as IVariableBrightnessTargetContainer;
                if (container != null) {
                    return container.ExoPlanetDSO;
                } else {
                    return RetrieveExoPlanetDSO(parent.Parent);
                }
            } else {
                return null;
            }
        }

        public static ExoPlanetInputTarget RetrieveInputTarget(ISequenceContainer parent) {
            if (parent != null) {
                var container = parent as IVariableBrightnessTargetContainer;
                if (container != null) {
                    return container.ExoPlanetInputTarget;
                } else {
                    return RetrieveInputTarget(parent.Parent);
                }
            } else {
                return null;
            }
        }

        public static void UpdateTakeExposureItems(ISequenceContainer parent, double exposureTime) {
            if (parent != null) {
                var items = parent.GetItemsSnapshot();
                foreach (var item in items) {
                    var listItem = item as TakeExposure;
                    if (listItem != null && listItem.ImageType == CaptureSequence.ImageTypes.LIGHT) {
                        listItem.ExposureTime = exposureTime;
                        continue;
                    }
                    var listSubItem = item as TakeSubframeExposure;
                    if (listSubItem != null && listSubItem.ImageType == CaptureSequence.ImageTypes.LIGHT) {
                        listSubItem.ExposureTime = exposureTime;
                        continue;
                    }

                    var container = item as ISequenceContainer;
                    if (container != null) {
                        UpdateTakeExposureItems(container, exposureTime);
                    }
                }
            }
        }
    }
}