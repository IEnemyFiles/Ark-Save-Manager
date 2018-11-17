﻿using ArkSaveEditor.Deserializer.DotArk;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArkSaveEditor.Entities.LowLevel.DotArk.ArkProperties
{
    public class ObjectProperty : DotArkProperty
    {
        public ObjectPropertyType objectRefType;

        public int objectId; //Only used if the above is ObjectPropertyType.TypeID
        public ArkClassName className; //Only used if the above is ObjectPropertyType.TypePath

        public ObjectProperty(DotArkDeserializer d, int index, int length)
        {
            var ms = d.ms;

            //If the length is four (only seems to happen on version 5), this is an integer.
            if(length == 4)
            {
                objectRefType = ObjectPropertyType.TypeID;
                objectId = ms.ReadInt();
            } else if (length >= 8)
            {
                //Read type
                int type = ms.ReadInt();
                if (type > 1 || type < 0)
                    throw new Exception($"Unknown ref type! Expected 0 or 1, but got {type} instead!");
                //Convert this to our enum
                objectRefType = (ObjectPropertyType)type;
                //Depending on the type, read it in.
                if (objectRefType == ObjectPropertyType.TypeID)
                    objectId = ms.ReadInt();
                if (objectRefType == ObjectPropertyType.TypePath)
                    className = ms.ReadArkClassname(d);
            } else
            {
                throw new Exception($"Unknown object ref length! Expected 4 or >= 8, but got {length} instead.");
            }

        }
    }

    public enum ObjectPropertyType
    {
        TypeID = 0,
        TypePath = 1
    }
}
