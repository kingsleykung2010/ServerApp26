using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Jayrock.Json;
using Jayrock.Json.Conversion;
using System.IO;

/*
 * +----------------------------+
 * |   JsonListUti              |
 * +----------------------------+
 * | (+) SplitList()            |
 * | (-) ParseObjTokens()       |
 * | (-) ParseNumberTokens()    |
 * | (-) ParseBooleanTokens()   |
 * | (-) ParseStringTokens()    |
 * | (-) ParseArrayTokens()     |
 * +----------------------------+
 * 
 */
class JsonListUti
{
    // PUBLIC UTILITY
    public static bool SplitList(string i_chunk, ref List<string> o_tokens)
    {
        if (i_chunk == "")
            return false;
        if (ParseObjTokens(i_chunk, ref o_tokens))
            return true;
        if (ParseNumberTokens(i_chunk, ref o_tokens))
            return true;
        if (ParseBooleanTokens(i_chunk, ref o_tokens))
            return true;
        if (ParseStringTokens(i_chunk, ref o_tokens))
            return true;
        if (ParseArrayTokens(i_chunk, ref o_tokens))
            return true;

        // #IFDEF DEBUG_JSON_LIST_UTI
        if (DEBUG_JSON_LIST_UTI)
        {
            Console.WriteLine("Invoking JsonListUti.SplitList() method.");
            Console.WriteLine("Splitting chunk into:");
            foreach (var iter in o_tokens)
            {
                Console.WriteLine("======================================");
                Console.WriteLine(iter);
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        return false;
    }

    // PRIVATE UTILITIES
    // (1) ParseObjTokens():
    // --  parse the list into tokens
    // --  tokens is gonna be passed out by reference as o_tokens
    // --  i_chunk is a JSON text, of the form "[{obj_1}, {obj_2}, ..., {obj_n}]"
    // --  which means, when the chunk is parsed into a JSON object, it's gonna be a list of objects
    // --  so, the method expects the first token of the i_chunk to be the JsonTokenClass.Array
    // --  if it's not the case, the method will return a false, and stop parsing for tokens righ away
    private static bool ParseObjTokens(string i_chunk, ref List<string> o_tokens)
    {
        using (JsonTextReader reader = new JsonTextReader(new StringReader(i_chunk)))
        {
            reader.Read(); // Check the first token

            // if the first token doesn't indicate the strart of an array
            if (reader.TokenClass != JsonTokenClass.Array)
                return false;

            // up to this point, we can be sure at least the chunk starts with JsonTokenClass.Array

            while (true)
            {
                reader.Read(); // Check the second token

                // At the beginning of the iteration, the previous token will be:
                // --   1) JsonTokenClass.Array
                // --   2) JsonTokenClass.EndObject
                // Either way, there are two possiblities:
                // --   1) the next token is JsonTokenClass.Object
                //         -> move onward to parsing the tokens
                // --   2) the next token is JsonTokenClass.EndArray
                //         -> break of the loop, and escapt the method
                // Else, it's considered to be a syntax error, and the method will return a false

                // if the second token doesn't indicate the start of an object
                if (reader.TokenClass == JsonTokenClass.EndArray)
                    break;
                else if (reader.TokenClass != JsonTokenClass.Object)
                    return false;

                // we can be sure that the second token is JsonTokenClass.Object
                // So, we create a temporary object
                // The object created is gonna act as a workbench
                // where we're gonna store our tokens onward

                // Workbench will be flushed every time we restart our iteration
                // If the syntax is somewhat, incorrect, the method might not push the token into the list, o_tokens
                using (JsonWriter workbench = new JsonTextWriter())
                {
                    workbench.WriteStartObject(); // Already known that the first token is JsonTokenClass.Object

                    // There are two break points
                    // first one is when reader.Read() return a null
                    // second one is when it's the end of the token
                    // The second one is always a normal breakpoint, except:
                    // --   a) When it's the end of the list, where "]" is encountered
                    //         -> In which case, the loop is gonna terminate, and the the method will return true.
                    //         -> Recall that there is a conditional statement:
                    //
                    //                     if (reader.TokenClass == JsonTokenClass.EndArray)
                    //                         break;
                    //
                    //            somewhere up there
                    // --   b) When the i_chunk read in has syntax error, in which case, the while (read.Read())
                    //         condition, prevent an infinite loop from happening

                    // We gotta keep a counter here, to take note of the number of JsonTokenClass.Object encountered
                    // we're gonna repeat the same parsing procedure 'till a JsonTokenClass.EndObject is encountered
                    // but that JsonTokenClass.EndObject might not necessarily corresponding to the first JsonTokenClass.Object
                    // which means, we have to keep track of the number of the same token encountered
                    // because, a JsonTokenClass.Object must have a corresponding JsonTokenClass.EndObject
                    uint tokenObjEncountered = 0;

                    while (reader.Read())
                    {
                        if (reader.TokenClass == JsonTokenClass.Array)
                            workbench.WriteStartArray();
                        else if (reader.TokenClass == JsonTokenClass.Boolean)
                            workbench.WriteBoolean(Convert.ToBoolean(reader.Text));
                        else if (reader.TokenClass == JsonTokenClass.EndArray)
                            workbench.WriteEndArray();
                        else if (reader.TokenClass == JsonTokenClass.EndObject)
                        {
                            workbench.WriteEndObject();
                            // need to be taken care here
                            // if the counter being kept is zero:
                            // (tokenObjEncounered would be incremented if JsonTokenClass.Object is encountered)
                            // (but, every time a JsonTokenClass.EndObject, which doesn't correspond to 
                            // the first JsonTokenClass.Object, is encountered, the counter would be decremented)
                            if (tokenObjEncountered == 0)
                            {
                                // which means this JsonTokenClas.EndObject corresponds to the first JsonTokenClass.Object
                                // Append the token into the list
                                o_tokens.Add(workbench.ToString());
                                break;
                            }
                            else
                            {
                                // It doesn't indicate the end of a token
                                tokenObjEncountered -= 1;
                            }
                        }
                        else if (reader.TokenClass == JsonTokenClass.Member)
                            workbench.WriteMember(reader.Text);
                        else if (reader.TokenClass == JsonTokenClass.Object)
                        {
                            // it indicates the start of an object
                            // JsonTokenClass.Object which is not at the beginning of the token
                            tokenObjEncountered += 1;
                            workbench.WriteStartObject();
                        }
                        else if (reader.TokenClass == JsonTokenClass.Number)
                            workbench.WriteNumber(reader.Text);
                        else if (reader.TokenClass == JsonTokenClass.String)
                            workbench.WriteString(reader.Text);
                    }
                }
                // END OF USING STATEMENT
            }
            // END OF ITERATION
        }
        // END OF USING STATEMENT
        return true;
    }

    // (2) ParseNumberTokens()
    // --  parse the list into Tokens
    // --  but this time, it assumes, that it's a list of number
    // --  return a false, when there is a syntax error
    private static bool ParseNumberTokens(string i_chunk, ref List<string> o_tokens)
    {
        using (JsonTextReader reader = new JsonTextReader(new StringReader(i_chunk)))
        {
            reader.Read(); // check the first token

            // if the first token doesn't indicate the start of an array
            if (reader.TokenClass != JsonTokenClass.Array)
                return false;

            // up to this point, we can be sure at least the chunk starts with JsonTokenClass.Array

            while (reader.Read())
            {
                // For each an every token
                // Only two possibilities are considered:
                // --  1) reader.TokenClass == JsonTokenClass.Number
                // --  2) reader.TokenClass == JsonTokenClass.EndArray
                //        -> Normal bread point of the loop
                //        -> Will return a true
                // Else, it's considered a syntax error

                if (reader.TokenClass == JsonTokenClass.Number)
                    o_tokens.Add(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.EndArray)
                    return true;
                else
                {
                    // Not return any tokens if there is an syntax error.
                    o_tokens.Clear();
                    return false;
                }
            }

            // Shouldn't have reached this point.
            // JsonTokenClass.EndArray should have been encountered
            o_tokens.Clear();
            return false;
        }
    }

    // (3) ParseBooleanTokens()
    // --  parse the list into Tokens
    // --  but this time, it assumes, that it's a list of boolean variables
    // --  return a false, when there's a syntax error
    private static bool ParseBooleanTokens(string i_chunk, ref List<string> o_tokens)
    {
        using (JsonTextReader reader = new JsonTextReader(new StringReader(i_chunk)))
        {
            reader.Read(); // check the first token

            // if the first token doesn't indicate the start of an array
            if (reader.TokenClass != JsonTokenClass.Array)
                return false;

            // up to this point, we can be sure at least the chunk starts with JsonTokenClass.Array

            while (reader.Read())
            {
                // For each an every token
                // Only two possibilities are considered:
                // --  1) reader.TokenClass == JsonTokenClass.Boolean
                // --  2) reader.TokenClass == JsonTokenClass.EndArray
                //        -> Normal break point of the loop
                //        -> Will return a true
                // Else, it's considered a syntax error

                if (reader.TokenClass == JsonTokenClass.Boolean)
                    o_tokens.Add(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.EndArray)
                    return true;
                else
                {
                    // Not return any tokens if there is an syntax error.
                    o_tokens.Clear();
                    return false;
                }
            }

            // Shouldn't have reached this point.
            // JsonTokenClass.EndArray should have been encountered
            o_tokens.Clear();
            return false;
        }
    }

    // (4) ParseStringTokens()
    // --  parse the list into tokens
    // --  this time, it assumes, that it's a list of strings
    // --  return a false when there's a syntax error
    private static bool ParseStringTokens(string i_chunk, ref List<string> o_tokens)
    {
        using (JsonTextReader reader = new JsonTextReader(new StringReader(i_chunk)))
        {
            reader.Read(); // check the first token

            // if the first token doesn't indicate the start of an array
            if (reader.TokenClass != JsonTokenClass.Array)
                return false;

            // up to this point, we can be sure at least the chunk starts with JsonTokenClass.Array

            while (reader.Read())
            {
                // For each an every token
                // Only two possibilities are considered:
                // --  1) reader.TokenClass == JsonTokenClass.String
                // --  2) reader.TokenClass == JsonTokenClass.EndArray
                //        -> Normal break point of the loop
                //        -> Will return a true
                // Else, it's considered a syntax error

                if (reader.TokenClass == JsonTokenClass.String)
                    o_tokens.Add(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.EndArray)
                    return true;
                else
                {
                    // Not return any tokens if there is an syntax error.
                    o_tokens.Clear();
                    return false;
                }
            }

            // Shouldn't have reached this point.
            // JsonTokenClass.EndArray should have been encountered
            o_tokens.Clear();
            return false;
        }
    }

    // (5) ParseArrayTokens():
    // --  parse the list into tokens
    // --  tokens is gonna be passed out by reference as o_tokens
    // --  i_chunk is a JSON text, of the form "[[list_1], [list_2], ..., [list_n]]"
    // --  which means, when the chunk is parsed into a JSON object, it's gonna be a nested list of lists
    // --  so, the method expects the first token of the i_chunk to be the JsonTokenClass.Array
    // --  if it's not the case, the method will return a false, and stop parsing for tokens righ away
    private static bool ParseArrayTokens(string i_chunk, ref List<string> o_tokens)
    {
        using (JsonTextReader reader = new JsonTextReader(new StringReader(i_chunk)))
        {
            reader.Read(); // Check the first token

            // if the first token doesn't indicate the strart of an array
            if (reader.TokenClass != JsonTokenClass.Array)
                return false;

            // up to this point, we can be sure at least the chunk starts with JsonTokenClass.Array

            while (true)
            {
                reader.Read(); // Check the second token

                // At the beginning of the iteration, the previous token will be:
                // --   1) JsonTokenClass.Array
                // --   2) JsonTokenClass.EndArray
                // Either way, there are two possiblities:
                // --   1) the next token is JsonTokenClass.Array
                //         -> move onward to parsing the tokens
                // --   2) the next token is JsonTokenClass.EndArray
                //         -> break of the loop, and escape the method
                // Else, it's considered to be a syntax error, and the method will return a false

                /*
                 * Why dun need to check the number of JsonTokenClass.Array encountered here?
                 * --------------------------------------------------------------------------
                 * Ans:  As if mentioned before, there are only 2 possibilities here. Either the
                 *       the previous token is JsonTokenClass.Array or JsonTokenClass.EndArray.
                 *       Remember that there is another loop down there somewhere, whose existence
                 *       is to parse a single token. So whenever the program has come to this place,
                 *       it means either the program has juz begun, or it has finished parsing it's 
                 *       previous token. If another JsonTokenClass.EndArray is encountered right here
                 *       we can tell that, this JsonTokenClass.EndArray, corresponds to the
                 *       the very first token of the list. Thus, indicates the end of parsing of the
                 *       whole list.
                 */

                // if the second token doesn't indicate the start of an array, return false
                if (reader.TokenClass == JsonTokenClass.EndArray)
                    break;
                else if (reader.TokenClass != JsonTokenClass.Array)
                    return false;

                // we can be sure that the second token is JsonTokenClass.Array
                // So, we create a temporary object
                // The object created is gonna act as a workbench
                // where we're gonna store our tokens onward

                // Workbench will be flushed every time we restart our iteration
                // If the syntax is somewhat, incorrect, the method might not push the token into the list, o_tokens
                using (JsonWriter workbench = new JsonTextWriter())
                {
                    workbench.WriteStartArray(); // Already known that the first token is gonna be JsonTokenClass.Array

                    // There are two break points
                    // first one is when reader.Read() return a null
                    // second one is when it's the end of the token
                    // The second one is always a normal breakpoint, except:
                    // --   a) When it's the end of the list, where "]" is encountered
                    //         -> In which case, the loop is gonna terminate, and the the method will return true.
                    //         -> Recall that there is a conditional statement:
                    //
                    //                     if (reader.TokenClass == JsonTokenClass.EndArray)
                    //                         break;
                    //
                    //            somewhere up there
                    // --   b) When the i_chunk read in has syntax error, in which case, the while (read.Read())
                    //         condition, prevent an infinite loop from happening

                    // We gotta keep a counter here, to take note of the number of JsonTokenClass.Array encountered
                    // we're gonna repeat the same parsing procedure 'till a JsonTokenClass.EndArray is encountered
                    // but that JsonTokenClass.EndArray might not necessarily correspond to the first JsonTokenClass.Array
                    // which means, we have to keep track of the number of the same token encountered
                    // because, a JsonTokenClass.Array must have a corresponding JsonTokenClass.EndArray
                    uint tokenArrEncountered = 0;

                    while (reader.Read())
                    {
                        if (reader.TokenClass == JsonTokenClass.Array)
                        {
                            // It indicates the start of another array
                            // Which is not at the beginning of the exterior array
                            tokenArrEncountered += 1;
                            workbench.WriteStartArray();
                        }
                        else if (reader.TokenClass == JsonTokenClass.Boolean)
                            workbench.WriteBoolean(Convert.ToBoolean(reader.Text));
                        else if (reader.TokenClass == JsonTokenClass.EndArray)
                        {
                            // write endArray no matter what
                            workbench.WriteEndArray();

                            // Need to be taken care over here
                            // if tokenArrEncountered is zero here
                            // it means that, no other JsonTokenClass.Array has been encountered
                            if (tokenArrEncountered == 0)
                            {
                                o_tokens.Add(workbench.ToString());
                                // So, we can break the loop
                                break;
                            }
                            else
                            {
                                // It is not the end of a single token
                                tokenArrEncountered -= 1;
                            }
                        }
                        else if (reader.TokenClass == JsonTokenClass.EndObject)
                            workbench.WriteEndObject();
                        else if (reader.TokenClass == JsonTokenClass.Member)
                            workbench.WriteMember(reader.Text);
                        else if (reader.TokenClass == JsonTokenClass.Object)
                            workbench.WriteStartObject();
                        else if (reader.TokenClass == JsonTokenClass.Number)
                            workbench.WriteNumber(reader.Text);
                        else if (reader.TokenClass == JsonTokenClass.String)
                            workbench.WriteString(reader.Text);
                    }
                }
            }
            // END OF ITERATION
        }
        // END OF USING STATEMENT
        return true;
    }

    // MACRO
    private static bool DEBUG_JSON_LIST_UTI = false;
}
