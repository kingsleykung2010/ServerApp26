using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Jayrock.Json;
using System.IO;


/*
 * +----------------------------+
 * | JsonObjectUti              |
 * +----------------------------+
 * | (+) GetTarget()            |
 * | (-) ContinueReadObject()   |
 * | (-) GeneralRead()          |
 * | (-) ContinueReadArray()    |
 * +----------------------------+
 */
class JsonObjectUti
{
    // (1) GetTarget()
    // --  Given a key and chunk, the method is going to search for the target being hashed wif the key
    // --  The target can be of any type
    // --  There're 2 scenarios where the method will return a false
    //     a) there is a syntax error
    //     b) key is not found
    // --  And the chunk is expected to be of an object/dictionary type
    public static bool GetTarget(string i_key, string i_chunk, ref string o_target)
    {
        if (i_chunk == "")
            return false;

        using (JsonTextReader reader = new JsonTextReader(new StringReader(i_chunk)))
        {
            if (!reader.Read())
                return false; // Check the first token

            // Make sure that the first token is a JsonTokenClass.Object
            if (reader.TokenClass != JsonTokenClass.Object)
                return false;

            // Up to this point, we can be sure that the fist token is of a JsonTokenClass.Object
            // Now, there're only two possibilities here:
            // 1)  Next token is of a JsonTokenClass.EndObject
            //     --> So, it's the end of parsing
            // 2)  Next token is of a JsonTokenClass.Member
            //     --> we're gonna continue searching for the right key
            // Else, it's considered a syntax error
            // Either way, we are gonna terminate the method: one way or another

            while (true)
            {
                if (!reader.Read()) // Check the second token
                    return false;

                // Syntax Error or End of Object (key not found)
                // Either way, return a false.
                if (reader.TokenClass != JsonTokenClass.Member || reader.TokenClass == JsonTokenClass.EndObject)
                    return false;

                // Now, it's the beginning of a new token.
                // And the current token is of the type JsonTokenReader.Member
                // So, we compare it wif the i_key given
                if (i_key == reader.Text)
                {
                    // Okay, target found.
                    // Check the next token
                    string tmp = "";
                    if (!GeneralRead(reader, ref tmp))
                    {
                        o_target = "";
                        return false;
                    }

                    // Now the target is successfully found.
                    // It can be a string, a number, a boolean variable, an object or an array
                    o_target = tmp;
                    return true;
                }
                else
                {
                    // Else: It's not the key we're looking for, move on to the next iteration.
                    // Since, it's not the key we're looking for, we're gonna skip and ignore the value
                    string buffer = "";

                    // If it's an object, read object, else, read token
                    // Methods are mutually exclusive
                    GeneralRead(reader, ref buffer);
                }
            }
        }
    }

    // PRIVATE UTILITIES
    // (2) ContinueReadObject()
    // --  reader is like a cursor
    // --  what I wanna do in this method is to move the cursor on to the right place
    // --  Our focus, should be on the outmost object
    // --  any member of the inner object should be wrapped up and passed out of the method
    // --  so, we are gonna wrap all the inner object into a chunk, and move the cursor to be after the chunk at the same time.

    // --  But, what tricky is, there is no turning back when it comes to moving the cursor
    // --  So, the first token shouldn't be read within the method itself
    // --  Here's the logic, the first token is read in a GeneralRead() method, which can be thought of as a wrapper function
    // --  The, wrapper function will then decide, whether to keep on reading as an object or array, or just stop as a token
    private static bool ContinueReadObject(JsonTextReader reader, ref string o_object)
    {
        /*
         * The following lines are gonna be implemented in the wrapper function
        // read the first token
        // if (!reader.Read())
        //   return false;
        */

        // As usual, we gotta make sure that the first token is of JsonTokenClass.Object
        // Besides, we're gonna keep a counter, objTokenEncountered, to keep tab of the number of JsonTokenClass.Object encountered
        uint objTokenEncountered = 0;
        if (reader.TokenClass != JsonTokenClass.Object)
            return false;

        // So now we know that the first token is of JsonTokenClass.Object
        // Create a workbench to write JSON string
        using (JsonWriter workbench = new JsonTextWriter())
        {
            workbench.WriteStartObject(); // Already known that the first token is of JsonTokenClass.Object

            // Now, we're gonna keep on reading tokens until a JsonTokenClass.EndObject
            // that corresponds to the first JsonTokenClass.Object is encountered
            while (reader.Read())
            {
                if (reader.TokenClass == JsonTokenClass.EndObject)
                {
                    workbench.WriteEndObject();

                    // need to be taken care of
                    if (objTokenEncountered == 0)
                    {
                        // Counter equals to zero indicates that such a token corresponds to the first JsonTokenClass.Object
                        // Thus, successfully finish parsing:
                        // finishing off
                        o_object = workbench.ToString();
                        return true;
                    }
                    else
                    {
                        // It's not the end of the object
                        // still need to keep parsing
                        objTokenEncountered -= 1;
                    }
                }
                else if (reader.TokenClass == JsonTokenClass.Object)
                {
                    workbench.WriteStartObject();
                    objTokenEncountered += 1;
                }
                else if (reader.TokenClass == JsonTokenClass.Array)
                    workbench.WriteStartArray();
                else if (reader.TokenClass == JsonTokenClass.Boolean)
                    workbench.WriteBoolean(Convert.ToBoolean(reader.Text));
                else if (reader.TokenClass == JsonTokenClass.EndArray)
                    workbench.WriteEndArray();
                else if (reader.TokenClass == JsonTokenClass.Member)
                    workbench.WriteMember(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.Number)
                    workbench.WriteNumber(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.String)
                    workbench.WriteString(reader.Text);
            }
        }// END OF USING STATEMENT
        return false; // syntax error
    }

    // (3) GeneralRead()
    // --  If an object is detected, o_chunk = object_retrieved
    // --  If an array is detected, o_chunk = array_retrieved
    // --  else, o_chunk = token.text
    private static bool GeneralRead(JsonTextReader reader, ref string o_chunk)
    {
        if (!reader.Read())
        {
            o_chunk = "";
            return false;
        }

        if (reader.TokenClass == JsonTokenClass.Object)
        {
            // Continue as object
            bool object_match_flag = ContinueReadObject(reader, ref o_chunk);

            return object_match_flag;
        }

        if (reader.TokenClass == JsonTokenClass.Array)
        {
            // Continue as array
            bool array_match_flag = ContinueReadArray(reader, ref o_chunk);

            
            return array_match_flag;
        }

        o_chunk = reader.Text;
        return true;
    }

    // (4) ContinueReadArray()
    // --  As usual, our focus shoudl be on the outermost array.
    // --  Any member of inner array should be wrapped up and passed out of the method.
    private static bool ContinueReadArray(JsonTextReader reader, ref string o_array)
    {
        // As usual, we gotta make sure that the first token is of JsonTokenClass.Array
        // Besides, we're gonna keep a counter, arrTokenEncountered, to keep tab of the number of JsonTokenClass.Array encountered
        uint arrTokenEncountered = 0;
        if (reader.TokenClass != JsonTokenClass.Array)
            return false;

        // So now we know that the first token is of JsonTokenClass.Object
        // Create a workbench to write JSON string
        using (JsonWriter workbench = new JsonTextWriter())
        {
            workbench.WriteStartArray(); // Already known that the first token is of JsonTokenClass.Array

            // Now, we're gonna keep on reading tokens until a JsonTokenClass.EndArray
            // that corresponds to the first JsonTokenClass.Array is encountered
            while (reader.Read())
            {
                if (reader.TokenClass == JsonTokenClass.EndArray)
                {
                    // Write EndArray no matter what
                    workbench.WriteEndArray();

                    // Check if the counter is zero
                    if (arrTokenEncountered == 0)
                    {
                        // Okay, reach the end of the token
                        // finishing up
                        o_array = workbench.ToString();
                        return true;
                    }
                    else
                    {
                        // Still need to keep parsing
                        arrTokenEncountered -= 1;
                    }
                }
                else if (reader.TokenClass == JsonTokenClass.Array)
                {
                    arrTokenEncountered += 1;
                    workbench.WriteStartArray();
                }
                else if (reader.TokenClass == JsonTokenClass.Object)
                    workbench.WriteStartObject();
                else if (reader.TokenClass == JsonTokenClass.EndObject)
                    workbench.WriteEndObject();
                else if (reader.TokenClass == JsonTokenClass.Boolean)
                    workbench.WriteBoolean(Convert.ToBoolean(reader.Text));
                else if (reader.TokenClass == JsonTokenClass.Member)
                    workbench.WriteMember(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.Number)
                    workbench.WriteNumber(reader.Text);
                else if (reader.TokenClass == JsonTokenClass.String)
                    workbench.WriteString(reader.Text);
            }
        } // END OF USING STATEMENT
        return false;
    }

    // MACRO
    private const bool DEBUG_JSON_OBJECT_UTI = false;
    private const bool DEBUG_JSON_OBJECT_UTI_OBJECT = false;
    private const bool DEBUG_JSON_OBJECT_UTI_ARRAY = false;
}
