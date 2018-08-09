#region License
/*
VPKSoft.TMDbFileUtils

A library to help enumerate both movie files and directories containing TV show seasons and the run them through the TMBb API.
Copyright © 2018 VPKSoft, Petteri Kautonen

Contact: vpksoft@vpksoft.net

This file is part of VPKSoft.TMDbFileUtils.

VPKSoft.TMDbFileUtils is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

VPKSoft.TMDbFileUtils is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with VPKSoft.TMDbFileUtils.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMdbEasy;
using TMdbEasy.ApiInterfaces;
using TMdbEasy.TmdbObjects.Movies;
using TMdbEasy.TmdbObjects.TV;
using VPKSoft.Utils;

namespace VPKSoft.TMDbFileUtils
{
    /// <summary>
    /// A class to contain a single movie or TV show details. This is used by the static TMDbFileEnumerator class.
    /// </summary>
    public class TMDbDetail
    {
        /// <summary>
        /// A TMDb movie or TV show ID.
        /// </summary>
        public int ID { get; set; } = -1;

        /// <summary>
        /// A TMDb TV show season ID.
        /// </summary>
        public int SeasonID { get; set; } = -1;

        /// <summary>
        /// A TMDb TV show episode ID.
        /// </summary>
        public int EpisodeID { get; set; } = -1;

        /// <summary>
        /// A TMDb movie or TV show title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// A TMDb movie or TV show description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A TMDb TV show episode description.
        /// </summary>
        public string DetailDescription { get; set; } = string.Empty;

        /// <summary>
        /// A full path to a local file.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// An Uri containing the URL of the TMDb movie or TV show season image.
        /// </summary>
        public Uri PosterOrStillURL { get; set; } = null;

        /// <summary>
        /// A TMBd season number of a TV show.
        /// </summary>
        public int Season { get; set; } = 0;

        /// <summary>
        /// A TMDB episode number of a TV show.
        /// </summary>
        public int Episode { get; set; } = 0;
    }

    /// <summary>
    /// A class to enumerate files from the file system and then get details for these files from the TMDb database.
    /// </summary>
    public static class TMDbFileEnumerator
    {
        /// <summary>
        /// Gets details for movie files from a given path from the TMDb database.
        /// </summary>
        /// <param name="easy">An instance to a TMdbEasy.EasyClient class instance.</param>
        /// <param name="path">A path to enumerate the files from. Do note that these files names should only contain the original title of the movie.</param>
        /// <param name="posterSize">The size of the poster to get the URL for.</param>
        /// <returns>A collection of TMDbDetail class instances containing the movie information from the TMDb database.</returns>
        public static IEnumerable<TMDbDetail> GetMovies(EasyClient easy, string path, string posterSize = "original")
        {
            return GetMoviesAsync(easy, path, posterSize).Result;
        }

        /// <summary>
        /// Gets details for movie files from a given path from the TMDb database.
        /// </summary>
        /// <param name="easy">An instance to a TMdbEasy.EasyClient class instance.</param>
        /// <param name="path">A path to enumerate the files from. Do note that these files names should only contain the original title of the movie.</param>
        /// <param name="posterSize">The size of the poster to get the URL for.</param>
        /// <returns>A collection of TMDbDetail class instances containing the movie information from the TMDb database.</returns>
        public static async Task<IEnumerable<TMDbDetail>> GetMoviesAsync(EasyClient easy, string path, string posterSize = "original")
        {
            var movieApi = easy.GetApi<IMovieApi>().Value; // create a IMovieApi..

            // List files of known video formats from the given path..
            IEnumerable<FileEnumeratorFileEntry> fileEntries = 
                await FileEnumerator.RecurseFilesAsync(path, FileEnumerator.FiltersVideoVlcNoBinNoIso).ConfigureAwait(false);

            // initialize the result..
            List<TMDbDetail> result = new List<TMDbDetail>();

            // loop through the files and try to get a details for them..
            foreach (FileEnumeratorFileEntry entry in fileEntries)
            {
                // query the movie from the TMDb database..
                MovieList list = await movieApi.SearchMoviesAsync(entry.FileNameNoExtension).ConfigureAwait(false);

                // if something was found..
                if (list != null && list.Total_results > 0)
                {
                    result.Add(new TMDbDetail() // return the details of the movie..
                    {
                        ID = list.Results[0].Id, // the first result is only taken into account..
                        Title = list.Results[0].Title, // set the title..
                        Description = list.Results[0].Overview, // set the overview..
                        FileName = entry.FileName, // set the file name..

                        // create an Uri for the poster path..
                        PosterOrStillURL = new Uri("https://image.tmdb.org/t/p/" + posterSize + list.Results[0].Poster_path)
                    });
                }
                else // nothing was found..
                {
                    result.Add(new TMDbDetail()
                    {
                        Title = entry.FileNameNoExtension, // the title can be the file name without path or extension..
                        Description = entry.FileNameNoExtension, // the description can be the file name without path or extension..
                        FileName = entry.FileName // set the file name..
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// Removes the season information from a given path. E.g. SomeShow Season 1.
        /// </summary>
        /// <param name="path">A path to make a valid search string from.</param>
        /// <returns>A string which should be suitable search string for the TMDb for a TV show.</returns>
        private static string GetTVShowSearchString(string path)
        {
            string searchString = new DirectoryInfo(path).Name; // the last path part..
            searchString = // multiple regular expressions as the programmer is not a guru with them..
                Regex.Replace(searchString, @"Season [0-9]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            searchString =
                Regex.Replace(searchString, @"S [0-9]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            searchString =
                Regex.Replace(searchString, @"S[0-9]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            searchString =
                Regex.Replace(searchString, @"[0-9]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
            return searchString; // return the string..
        }
        
        /// <summary>
        /// Gets a TV show episode number of a given path string.
        /// </summary>
        /// <param name="path">A path string to get the episode number from.</param>
        /// <returns>A TV show episode number.</returns>
        private static int GetTVShowEpisodeNumber(string path)
        {
            string searchString = new DirectoryInfo(path).Name; // the last path part..

            string episodeNum = string.Empty; // first assume empty

            // multiple regular expressions as the programmer is not a guru with them..
            if ((episodeNum = Regex.Match(searchString, @"E\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Value).TrimStart('E', 'e') != string.Empty)
            {
                if (int.TryParse(episodeNum, out _)) // check for a valid integer..
                {
                    return int.Parse(episodeNum); // ..if the integer was valid, return it..
                }
            }
            if ((episodeNum = Regex.Match(searchString, @"X\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Value).TrimStart('X', 'x') != string.Empty)
            {
                if (int.TryParse(episodeNum, out _)) // check for a valid integer..
                {
                    return int.Parse(episodeNum); // ..if the integer was valid, return it..
                }
            }

            if ((episodeNum = Regex.Match(searchString, @"\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Value) != string.Empty)
            {
                if (int.TryParse(episodeNum, out _)) // check for a valid integer..
                {
                    return int.Parse(episodeNum); // ..if the integer was valid, return it..
                }
            }
            return -1; // just return -1..
        }
        
        /// <summary>
        /// Gets the TV show season number from a given path string.
        /// </summary>
        /// <param name="path">A path string to extract the season number from</param>
        /// <returns></returns>
        private static int GetTVShowSeason(string path)
        {
            string searchString = new DirectoryInfo(path).Name; // the last path part..

            searchString = Regex.Match(searchString, @"-?\d+").Value; // find a number from the path part..
            if (int.TryParse(searchString, out _)) // check for a valid integer..
            {
                return int.Parse(searchString); // ..if the integer was valid, return it..
            }
            else
            {
                return -1; // else just return -1..
            }
        }

        /// <summary>
        /// A list of possible naming conventions for a TV show episode file name.
        /// </summary>
        public static List<string> EpisodeNamingStyles { get; set; } = 
            new List<string>(
                new string[] 
                {
                    "s{0:00}e{1:00}", // SomeShow S01E01.*
                    "s{0}e{1}",       // SomeShow S1E1.*
                    "s{0}e{1:00}",    // SomeShow S1E01.*
                    "s{0:00}e{1}",    // SomeShow S01E1.*
                    "{0:00}x{1:00}",  // SomeShow 01x01.*
                    "{0}x{1:00}",     // SomeShow 1x01.*
                    "{0}x{1}",        // SomeShow 1x1.*
                    "{0:00}x{1}"      // SomeShow 01x1.*
                });

        /// <summary>
        /// Finds a matching file name from a give TV show season and episode number.
        /// </summary>
        /// <param name="fileEntries">A collection of FileEnumeratorFileEntry class instances to search for a match.</param>
        /// <param name="season">A season number to be used in the search.</param>
        /// <param name="episode">An episode number to be used in the search.</param>
        /// <returns>A matching file name from a give TV show season and episode number from the given collection.</returns>
        private static FileEnumeratorFileEntry GetFileNameMatchingTVSeasonEpisode(IEnumerable<FileEnumeratorFileEntry> fileEntries, int season, int episode)
        {
            FileEnumeratorFileEntry result = null; // nothing might be found..
            foreach (string namingStyle in EpisodeNamingStyles) // loop through the given naming styles..
            {
                try // as the string format might be invalid, lets not cause the operation to fail because of that..
                {
                    result = // try to get a result from the given collection..
                        fileEntries.FirstOrDefault(f => f.FileNameNoExtension.ToLowerInvariant().Contains(string.Format(namingStyle, season, episode)));
                    if (result != null) // if found..
                    {
                        return result; // ..return it..
                    }
                }
                catch // just an empty catch - there is no hope..
                {

                }
            }
            result = // last try is just the episode number..
                fileEntries.FirstOrDefault(f => f.FileNameNoExtension.Contains(episode.ToString()));

            return result; // return the result whether it's null or not..
        }

        /// <summary>
        /// A format of how to give a title to a TV show episode. It must have four parameters.
        /// The format is as {0} is the name of the series, {1} is the name of the season, {2} is the number of the episode
        /// and {3} is the name of the episode.
        /// <note type="note">Exceptions will occur if the format is incorrect.</note>
        /// </summary>
        public static string TVEpisodeFormat { get; set; } = "{0} {1}, Episode {2} - {3}";

        /// <summary>
        /// Searches the TMDb database for a TV season based on a given path using a TMdbEasy.EasyClient class instance.
        /// </summary>
        /// <param name="easy">A TMdbEasy.EasyClient class instance to use for the search.</param>
        /// <param name="path">A path to enumerate files from.</param>
        /// <param name="stillSize">The size of the still image to get the URL for.</param>
        /// <returns>A collection of TMDbDetail class instances containing the TV show season information from the TMDb database.</returns>
        public static IEnumerable<TMDbDetail> GetSeason(EasyClient easy, string path, string stillSize = "original")
        {
            return GetSeasonAsync(easy, path, stillSize).Result;
        }

        /// <summary>
        /// Searches the TMDb database for a TV season based on a given path using a TMdbEasy.EasyClient class instance.
        /// </summary>
        /// <param name="easy">A TMdbEasy.EasyClient class instance to use for the search.</param>
        /// <param name="path">A path to enumerate files from.</param>
        /// <param name="stillSize">The size of the still image to get the URL for.</param>
        /// <returns>A collection of TMDbDetail class instances containing the TV show season information from the TMDb database.</returns>
        public static async Task<IEnumerable<TMDbDetail>> GetSeasonAsync(EasyClient easy, string path, string stillSize = "original")
        {
            var televisionApi = easy.GetApi<ITelevisionApi>().Value; // create a ITelevisionApi..

            // List files of known video formats from the given path..
            IEnumerable<FileEnumeratorFileEntry> fileEntries =
                await FileEnumerator.RecurseFilesAsync(path, FileEnumerator.FiltersVideoVlcNoBinNoIso).ConfigureAwait(false);

            // don't start searching if the directory is empty - we don't want to cause excess stress for the TMDb database..
            if (fileEntries.ToList().Count == 0)
            {
                // ..so just throw an exception..
                throw new Exception("No files were found from the given path string.");
            }

            // construct a search string of the given path..
            string searchString = GetTVShowSearchString(path);
            int season = GetTVShowSeason(path); // extract a season number from the given path..

            if (season == -1) // if no season number was in the given path..
            {
                // ..just throw an exception..
                throw new Exception("The TV season number wasn't found of the given path string.");
            }

            // initialize the result..
            List<TMDbDetail> result = new List<TMDbDetail>();

            // search for TV shows base on the search string build from the given directory name..
            TVShowList list = await televisionApi.SearchTVShowsAsync(searchString).ConfigureAwait(false);

            // if something was found..
            if (list != null && list.Total_results > 0) // ..deepen the search..
            {
                string seriesName = list.Results[0].Name; // save the name of the TV show..
                int seriesID = list.Results[0].Id; // save the ID of the TV show..

                // find the TV show season from the TMDb database with an ID and a season number..
                TvSeason tvSeason = await televisionApi.GetSeasonDetailsAsync(seriesID, season).ConfigureAwait(false);

                // if something was found..
                if (tvSeason != null && tvSeason.Episodes != null)
                {
                    foreach (Episode episode in tvSeason.Episodes) // return the details of the TV show season..
                    {
                        // don't return a file-less TMDbDetail class instance..
                        if (GetFileNameMatchingTVSeasonEpisode(fileEntries, season, episode.Episode_number) == null)
                        {
                            continue; // ..so just continue the loop..
                        }

                        result.Add(new TMDbDetail
                        {
                            ID = seriesID, // the TMDb id for the TV show..
                            SeasonID = tvSeason.Id, // the TMDb id for the TV show season..
                            EpisodeID = episode.Id, // the TMDb id for the TV show season episode..

                            // formulate the title base on the TVEpisodeFormat property value..
                            Title = string.Format(TVEpisodeFormat, seriesName, tvSeason.Name, episode.Episode_number, episode.Name),

                            // set the description..
                            Description = string.IsNullOrEmpty(tvSeason.Overview) ? episode.Overview : tvSeason.Overview,
                            DetailDescription = episode.Overview, // set the detailed description if any..

                            // find the file name for the TV show episode..
                            FileName = GetFileNameMatchingTVSeasonEpisode(fileEntries, season, episode.Episode_number).FileName,

                            // create an URL for the still using the TV season's poster path as a "fail safe"..
                            PosterOrStillURL = new Uri("https://image.tmdb.org/t/p/" + stillSize +
                                (string.IsNullOrEmpty(episode.Still_path) ? tvSeason.Poster_path : episode.Still_path)),
                            Season = season, // set the season number..
                            Episode = episode.Episode_number // set the episode number..
                        });
                    }
                }
                else // nothing was found..
                {
                    // loop through the found files..
                    foreach (FileEnumeratorFileEntry entry in fileEntries)
                    {
                        result.Add(new TMDbDetail
                        {
                            Title = entry.FileNameNoExtension, // the title can be the file name without path or extension..
                            FileName = entry.FileName, // set the file name..
                            Season = season, // set the season number.. 
                            Episode = GetTVShowEpisodeNumber(entry.FileName) // set the episode number..                
                        });
                    }
                }
            }
            return result;
        }
    }
}
