using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace DeathMap
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("[DeathMap] no params");
                Console.WriteLine("[DeathMap] provide directory path with admin logs inside (*.ADM)");
                Console.ReadKey();
            }
            else
            {
                // load config file (hardcoded name DeathMap.cfg, has to be near the exe file)
                string configMapSize = "", configOutputSize = "", configOutputType = "", configOutputContent = "", configColorScale = "", configColorPallete = "";
                using (var reader = new StreamReader(File.Open("DeathMap.cfg", FileMode.Open)))
                {
                    // map size or name
                    var line = reader.ReadLine();
                    var splitLine = line.Split('=');
                    configMapSize = splitLine[1];
                    // output size
                    line = reader.ReadLine();
                    splitLine = line.Split('=');
                    configOutputSize = splitLine[1];
                    // heatmap or pixel
                    line = reader.ReadLine();
                    splitLine = line.Split('=');
                    configOutputType = splitLine[1];
                    // output content (all player deaths, deaths from infected,..)
                    line = reader.ReadLine();
                    splitLine = line.Split('=');
                    configOutputContent = splitLine[1];
                    // output scale (linear or logarithmic)
                    line = reader.ReadLine();
                    splitLine = line.Split('=');
                    configColorScale = splitLine[1];
                    // color pallete (for heatmap only)
                    line = reader.ReadLine();
                    splitLine = line.Split('=');
                    configColorPallete = splitLine[1];

                    reader.Close();
                }

                // process world info (so we know relative positions of data in admlog)
                var mapSize = -1;
                if (int.TryParse(configMapSize, out mapSize))
                {
                }
                else
                {
                    switch (configMapSize.ToLower())
                    {
                        case "livonia":
                            mapSize = 12800;
                            break;
                        case "enoch":
                            mapSize = 12800;
                            break;
                        case "chernarus":
                            mapSize = 15360;
                            break;
                        case "chernarusplus":
                            mapSize = 15360;
                            break;
                        case "namalsk":
                            mapSize = 12800;
                            break;
                        case "deerisle":
                            mapSize = 16384;
                            break;
                        case "exclusionzone":
                            mapSize = 20480;
                            break;
                        default:
                            mapSize = -1;
                            Console.WriteLine("[DeathMap] unrecognized world name");
                            Console.WriteLine("[DeathMap] press any key to abort...");
                            Console.ReadKey();
                            break;
                    }
                }

                if (mapSize != -1)
                {
                    Console.WriteLine("[DeathMap] worldSize is " + mapSize);

                    // process all input admin log for player death positions
                    var positionArray = new List<float>();
                    int numberOfDeaths = 0, outsideBounds = 0;
                    var stopLineSearch = false;
                    char[] wantedSeq = { 'p', 'o', 's', '=', '<' };

                    // these strings will be used to search lines we want
                    string file_test1, file_test2;
                    switch (configOutputContent)
                    {
                        case "all":
                            file_test1 = ">) killed by ";
                            file_test2 = "died. Stats>";
                            break;
                        case "infected":
                            file_test1 = ">) killed by ZmbF";
                            file_test2 = ">) killed by ZmbM";
                            break;
                        default:
                            file_test1 = ">) killed by ";
                            file_test2 = "died. Stats>";
                            break;
                    }

                    // go through ALL *.ADM files from specified directory
                    var files = Directory.GetFiles(args[0], "*", SearchOption.AllDirectories);
                    foreach (var fileName in files)
                    {
                        if (fileName.Contains(".ADM"))
                        {
                            using (var reader = new StreamReader(File.Open(fileName, FileMode.Open)))
                            {
                                while (reader.Peek() >= 0)
                                {
                                    var line = reader.ReadLine();
                                    if (line.Contains(file_test1) || line.Contains(file_test2))
                                    {
                                        stopLineSearch = false;
                                        var positionString = "";
                                        var j = 0;
                                        for (var i = 0; i < line.Length; i++)
                                        {
                                            if (stopLineSearch)
                                                break;

                                            if (j < 5)
                                            {
                                                // find pos=<
                                                if (line[i] == wantedSeq[j])
                                                {
                                                    j++;
                                                }
                                                else
                                                {
                                                    j = 0;
                                                }
                                            }
                                            else
                                            {
                                                // get position
                                                if (line[i] != '>')
                                                {
                                                    positionString += line[i];
                                                }
                                                else
                                                {
                                                    stopLineSearch = true;
                                                    numberOfDeaths++;
                                                }
                                            }
                                        }

                                        // parse position
                                        var positionStringSplit = positionString.Split(',');
                                        float posParsedX = float.Parse(positionStringSplit[0], CultureInfo.InvariantCulture.NumberFormat), posParsedY = float.Parse(positionStringSplit[1], CultureInfo.InvariantCulture.NumberFormat);
                                        if (posParsedX >= 0 && posParsedX < mapSize && posParsedY >= 0 && posParsedY < mapSize)
                                        {
                                            positionArray.Add(posParsedX);
                                            positionArray.Add(posParsedY);
                                        }
                                        else
                                        {
                                            outsideBounds++;
                                        }
                                    }
                                }
                                reader.Close();

                                Console.WriteLine("[DeathMap] " + fileName + " processed");
                            }
                        }
                    }
                    Console.WriteLine("[DeathMap] total number of registered deaths is " + numberOfDeaths + " (outside of bounds " + outsideBounds + ")");

                    // generate heat or pixel map
                    int posX, posY;
                    var outputSize = int.Parse(configOutputSize);
                    if (outputSize < 100)
                        outputSize = 100;

                    switch (configOutputType)
                    {
                        case "heat":
                            // initialize intensityGrid with given size
                            var intensityGrid = new float[outputSize, outputSize];
                            for (var i = 0; i < outputSize; i++)
                                for (var j = 0; j < outputSize; j++)
                                    intensityGrid[i, j] = 0.0f;

                            for (var i = 0; i < positionArray.Count - 1; i = i + 2)
                            {
                                // 2D position within intensityGrid based on the input death coords
                                posX = (int)(positionArray[i] / mapSize * outputSize);
                                posY = outputSize - 1 - (int)(positionArray[i + 1] / mapSize * outputSize);

                                // relative size of "dot" to the output image size
                                var sizeOfDot = (int)(0.075 * outputSize);
                                if (sizeOfDot % 2 != 0)
                                {
                                    sizeOfDot -= 1;
                                }

                                // border conditions
                                var bxmax = Math.Min(posX + sizeOfDot / 2 + 1, outputSize);
                                var bxmin = Math.Max(0, posX - sizeOfDot / 2);
                                var bymax = Math.Min(posY + sizeOfDot / 2 + 1, outputSize);
                                var bymin = Math.Max(0, posY - sizeOfDot / 2);

                                // https://en.wikipedia.org/wiki/Normal_distribution
                                var sigma = sizeOfDot / 7.0f;
                                for (var x = bxmin; x < bxmax; x++)
                                {
                                    for (var y = bymin; y < bymax; y++)
                                    {
                                        intensityGrid[x, y] += (float)(Math.Exp(-1 * ((x - posX) * (x - posX) + (y - posY) * (y - posY)) / 2.0 / sigma / sigma) / Math.Sqrt(sigma * sigma / 2.0 / Math.PI));
                                    }
                                }
                            }

                            // find maximum in intensityGrid (to use for colorizing later)
                            var max = 0.0f;
                            foreach (var e in intensityGrid)
                            {
                                if (e > max)
                                    max = e;
                            }

                            // load colors from color pallete
                            var pallete = new Color[256];
                            using (var colorPallete = new Bitmap(configColorPallete))
                            {
                                for (var i = 0; i < 256; i++)
                                {
                                    pallete[i] = colorPallete.GetPixel(i, 0);
                                }
                            }

                            // draw intensityGrid into bitmap
                            var linear = true;
                            if (configColorScale == "log")
                                linear = false;
                            using (var outputHeatMap = new Bitmap(outputSize, outputSize))
                            {
                                for (var i = 0; i < outputSize; i++)
                                {
                                    for (var j = 0; j < outputSize; j++)
                                    {
                                        var colorIndex = 1;
                                        if (linear)
                                            colorIndex = (int)(intensityGrid[i, j] / max * 255);
                                        else
                                            colorIndex = (int)(Math.Log(intensityGrid[i, j] + 1) / Math.Log(max + 1) * 255);
                                        var pixelColor = pallete[colorIndex];
                                        outputHeatMap.SetPixel(i, j, pixelColor);
                                    }
                                }
                                outputHeatMap.Save("DeathMap_HeatMap.bmp", ImageFormat.Bmp);
                            }
                            break;

                        case "pixel":
                            // initialize dataGrid (fixed size of 100, which for chernarus means roughly each 150 meters)
                            var dataGridSize = 100;
                            var dataGrid = new int[dataGridSize * dataGridSize];
                            for (var i = 0; i < dataGrid.Length; i++)
                                dataGrid[i] = 0;

                            // go through all positions and fill dataGrid with number of deaths per grid tile (increment)
                            for (var i = 0; i < positionArray.Count - 1; i = i + 2)
                            {
                                posX = (int)(positionArray[i] / mapSize * dataGridSize);
                                posY = dataGridSize - 1 - (int)(positionArray[i + 1] / mapSize * dataGridSize);
                                dataGrid[posY * dataGridSize + posX]++;
                            }

                            // find max value in dataGrid (to be used later for colorizing pixel map)
                            var maxx = 0;
                            foreach (var e in dataGrid)
                            {
                                if (e > maxx)
                                    maxx = e;
                            }

                            // draw dataGrid into bitmap
                            using (var outputPixelMap = new Bitmap(outputSize, outputSize))
                            {
                                var truePixelSize = outputSize / dataGridSize;
                                for (var i = 0; i < dataGrid.Length; i++)
                                {
                                    int picPosX = i % dataGridSize, picPosY = i / dataGridSize;

                                    Color pixelColor;
                                    if (dataGrid[i] != 0)
                                    {
                                        pixelColor = Color.Brown;
                                        if (dataGrid[i] > maxx / 6)
                                            pixelColor = Color.Orange;
                                        if (dataGrid[i] > maxx / 3)
                                            pixelColor = Color.Yellow;
                                        if (dataGrid[i] >= maxx)
                                            pixelColor = Color.White;
                                    }
                                    else
                                    {
                                        pixelColor = Color.Black;
                                    }

                                    for (var j = 0; j < truePixelSize; j++)
                                    {
                                        for (var k = 0; k < truePixelSize; k++)
                                        {
                                            outputPixelMap.SetPixel(picPosX * truePixelSize + j, picPosY * truePixelSize + k, pixelColor);
                                        }
                                    }
                                }

                                outputPixelMap.Save("DeathMap_PixelMap.bmp", ImageFormat.Bmp);
                            }
                            break;
                    }
                }
            }
        }
    }
}