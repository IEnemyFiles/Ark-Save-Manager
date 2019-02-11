﻿using ArkSaveEditor.Deserializer.DotArk;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArkSaveEditor.Entities.LowLevel.DotArk.ArkProperties
{
    public class ArkStructLinearColor : DotArkStruct
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public ArkStructLinearColor(DotArkDeserializer d, ArkClassName structType)
        {
            var ms = d.ms;

            r = ms.ReadFloat();
            g = ms.ReadFloat();
            b = ms.ReadFloat();
            a = ms.ReadFloat();
        }
    }
}