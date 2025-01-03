﻿using System;
using System.Collections.Generic;
using System.Text;
using ArkSaveEditor.Entities.LowLevel;
using ArkSaveEditor.Entities.LowLevel.DotArk;
using System.Linq;
using ArkSaveEditor.Entities.LowLevel.AutoGeneratedClasses;
using ArkSaveEditor.World.WorldTypes;
using ArkSaveEditor.Entities;
using Newtonsoft.Json;
using System.IO;
using ArkSaveEditor.ArkEntries;

namespace ArkSaveEditor.World
{
    /// <summary>
    /// Provides high-level access to the Ark saves. These will need to be able to be saved back to a file.
    /// </summary>
    public class ArkWorld
    {
        /// <summary>
        /// The name of the map (for example, "Extinction", "Aberration")
        /// </summary>
        public string map;

        /// <summary>
        /// Time of day in-game.
        /// </summary>
        public float gameTime;

        /// <summary>
        /// The number of days that have passed.
        /// </summary>
        public int day;

        //GameObjects
        /// <summary>
        /// This contains the internal array of sources. Some of these could be null if they've been deleted.
        /// </summary>
        public List<DotArkGameObject> sources { get; }

        /// <summary>
        /// Info about the actual Ark map
        /// </summary>
        public ArkMapData mapinfo;

        /// <summary>
        /// Grabs all standard GameObjects by their classname.
        /// </summary>
        /// <param name="classname">The string classname.</param>
        /// <returns></returns>
        public List<HighLevelArkGameObjectRef> GetGameObjectsByClassname(string classname)
        {
            List<HighLevelArkGameObjectRef> refs = new List<HighLevelArkGameObjectRef>();
            //Find all GameObjects in the sources. We can't use Linq because we need the the indexes.
            for (int i = 0; i<sources.Count; i++)
            {
                var src = sources[i];
                if(src != null)
                {
                    if (src.classname.classname == classname)
                        refs.Add(new HighLevelArkGameObjectRef(this, i));
                }
            }
            return refs;
        }

        /// <summary>
        /// Cached population. Call GetPopulation to refresh.
        /// </summary>
        public List<ArkDinosaur>[,] cached_population;

        /// <summary>
        /// Find dino populations throughout the map
        /// </summary>
        /// <param name="bounds">Where to search. IF NULL, map data will be used instead.</param>
        /// <param name="tile_axis_count">Number of tiles on one axis.</param>
        /// <returns></returns>
        public List<ArkDinosaur>[,] GetPopulation(int tile_axis_count, WorldBounds2D bounds = null)
        {
            //Get bounds if needed
            if (bounds == null)
                bounds = mapinfo.bounds;

            //Create this array
            List<ArkDinosaur>[,] output = new List<ArkDinosaur>[tile_axis_count, tile_axis_count];

            //Find the sizes
            float xSize = (float)bounds.width / (float)tile_axis_count;
            float ySize = (float)bounds.height / (float)tile_axis_count;

            //Loop through each dino
            foreach (var dino in dinos)
            {
                //Get dino pos
                float normalizedX = dino.location.x - bounds.minX;
                float normalizedY = dino.location.y - bounds.minY;

                //Find which "cell" this dino is in
                float cellX = normalizedX / xSize;
                float cellY = normalizedY / ySize;

                //Normalize the cells so we can apply the transforms
                //Apply transforms
                Vector2 adjusted = mapinfo.ConvertFromGamePositionToNormalized(new Vector2(dino.location.x, dino.location.y));

                //Reexpand
                cellX = adjusted.x * tile_axis_count;
                cellY = adjusted.y * tile_axis_count;

                //Round cell ID
                int cellIdX = (int)Math.Round(cellX);
                int cellIdY = (int)Math.Round(cellY);

                //Check if in bounds
                if (cellIdX < 0 || cellIdY < 0 || cellIdX >= tile_axis_count || cellIdY >= tile_axis_count)
                {
                    continue;
                }

                //If a list does not exist at this location, create one
                if (output[cellIdX, cellIdY] == null)
                    output[cellIdX, cellIdY] = new List<ArkDinosaur>();
                output[cellIdX, cellIdY].Add(dino);
            }
            cached_population = output;
            return output;
        }

        public Vector2 ConvertFromWorldToGameCoords(DotArkLocationData src)
        {
            return new Vector2(ConvertSingleFromWolrdToGameCoords(src.x), ConvertSingleFromWolrdToGameCoords(src.y));
        }

        public Vector2 ConvertFromWorldToNormalizedPos(DotArkLocationData src)
        {
            return new Vector2(ConvertFromWorldToNormalizedPos(src.x), ConvertFromWorldToNormalizedPos(src.y));
        }

        /// <summary>
        /// Bottom left: 0, Top right: 1
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public float ConvertFromWorldToNormalizedPos(float src)
        {
            //Add half of the width to make it start at zero.
            float r = src + (mapinfo.bounds.height / 2);

            //Now divide by the length of the map
            return r / mapinfo.bounds.height;
        }

        public float ConvertSingleFromWolrdToGameCoords(float src)
        {
            return (src / mapinfo.latLonMultiplier) + 50;
        }

        //GameObject types
        public List<ArkDinosaur> dinos = new List<ArkDinosaur>();
        public List<ArkStructure> structures = new List<ArkStructure>();
        public List<ArkPlayerProfile> players = new List<ArkPlayerProfile>();
        public List<ArkTribeProfile> tribes = new List<ArkTribeProfile>();
        public List<ArkPlayer> playerCharacters = new List<ArkPlayer>();

        //Settings
        public ArkConfigSettings configSettings;

        /// <summary>
        /// Convert the map from a low-level object to a high-level object.
        /// </summary>
        /// <param name="savePath">The path to the folder housing the game data.</param>
        /// <param name="mapFileName">The name of the map file. For example, "Extinction" if you would like to load "Extinction.ark".</param>
        /// <param name="overrideMapData">Override the map data.</param>
        public ArkWorld(string savePath, string mapFileName, string configPath, ArkMapData overrideMapData = null, bool loadOnlyKnown = false)
        {
            //Read the config files
            configSettings = new ArkConfigSettings();
            configSettings.ReadFromFile(File.ReadAllLines(configPath + "Game.ini"));
            configSettings.ReadFromFile(File.ReadAllLines(configPath + "GameUserSettings.ini"));

            //Open the .ark file now.
            string arkFileLocaton = Path.Combine(savePath, mapFileName + ".ark");
            var arkFile = ArkSaveEditor.Deserializer.ArkSaveDeserializer.OpenDotArk(arkFileLocaton);

            //Loop through all .arkprofile files here and add them to the player list.
            string[] saveFilePaths = Directory.GetFiles(savePath);
            foreach(string profilePathname in saveFilePaths)
            {
                try
                {
                    if (profilePathname.ToLower().EndsWith(".arkprofile"))
                    {
                        //This is an ARK profile. Open it.
                        players.Add(ArkPlayerProfile.ReadFromFile(profilePathname));
                    }
                    if (profilePathname.ToLower().EndsWith(".arktribe"))
                    {
                        //This is an ARK tribe. Open it.
                        tribes.Add(new ArkTribeProfile(profilePathname, this));
                    }
                } catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load an Ark tribe or player at {profilePathname.Substring(savePath.Length)}. It will be skipped. This could result in errors!");
                    Console.WriteLine($"Error debug data: {ex.Message} - {ex.StackTrace}");
                }
            }

            //Check to see if we have imported data. This will crash if we have not.
            if (ArkImports.dino_entries == null)
                throw new Exception("Missing ArkImports.dino_entries! You should run ArkImports.ImportContent() to import the content.");
            if (ArkImports.item_entries == null)
                throw new Exception("Missing ArkImports.item_entries! You should run ArkImports.ImportContent() to import the content.");
            if (ArkImports.world_settings == null)
                throw new Exception("Missing ArkImports.world_settings! You should run ArkImports.ImportContent() to import the content.");

            //Set our sources first
            sources = arkFile.gameObjects;
            //Do some analysis to find objects
            for(int i = 0; i<sources.Count; i++)
            {
                var g = sources[i];
                string classname = g.classname.classname;
                bool known = false;

                //Check if this is a dinosaur by matching the classname.
                if (Enum.TryParse<DinoClasses>(classname, out DinoClasses dinoClass))
                {
                    //This is a dinosaur.
                    dinos.Add(new ArkDinosaur(this, this.sources[i]));
                    known = true;
                }

                //Check if this is a player
                if(classname == "PlayerPawnTest_Male_C" || classname == "PlayerPawnTest_Female_C")
                {
                    //This is a player.
                    playerCharacters.Add(new ArkPlayer(this, this.sources[i]));
                    known = true;
                }

                //Check if this is a structure
                StructureDisplayMetadata metadata = ArkImports.GetStructureDisplayMetadataByClassname(classname);
                if(metadata != null)
                {
                    //This is a structure.
                    structures.Add(new ArkStructure(this, this.sources[i], metadata));
                    known = true;
                }
            }

            //Get the other metadata
            map = arkFile.meta.binaryDataNames[0];
            gameTime = arkFile.gameTime;

            //If we got the override map data, set it. Else, auto detect
            if(overrideMapData == null)
            {
                //Autodetect
                if (ArkMapDataTable.arkmaps.ContainsKey(map))
                    mapinfo = ArkMapDataTable.arkmaps[map];
                else
                    throw new Exception($"Ark map, '{map}', could not be found in the ArkMapDataTable. Please provide an override map data in the constructor for the ArkWorld.");
            } else
            {
                mapinfo = overrideMapData;
            }
        }
    }
}
