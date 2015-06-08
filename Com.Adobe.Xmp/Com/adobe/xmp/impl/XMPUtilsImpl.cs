// =================================================================================================
// ADOBE SYSTEMS INCORPORATED
// Copyright 2006 Adobe Systems Incorporated
// All Rights Reserved
//
// NOTICE:  Adobe permits you to use, modify, and distribute this file in accordance with the terms
// of the Adobe license agreement accompanying it.
// =================================================================================================

using System.Diagnostics;
using System.Text;
using Com.Adobe.Xmp.Impl.Xpath;
using Com.Adobe.Xmp.Options;
using Com.Adobe.Xmp.Properties;
using Sharpen;

namespace Com.Adobe.Xmp.Impl
{
    /// <since>11.08.2006</since>
    public class XMPUtilsImpl : XMPConst
    {
        private const int UckNormal = 0;

        private const int UckSpace = 1;

        private const int UckComma = 2;

        private const int UckSemicolon = 3;

        private const int UckQuote = 4;

        private const int UckControl = 5;

        /// <summary>Private constructor, as</summary>
        private XMPUtilsImpl()
        {
        }

        // EMPTY
        /// <seealso cref="Com.Adobe.Xmp.XMPUtils.CatenateArrayItems(Com.Adobe.Xmp.XMPMeta, string, string, string, string, bool)"/>
        /// <param name="xmp">The XMP object containing the array to be catenated.</param>
        /// <param name="schemaNS">
        /// The schema namespace URI for the array. Must not be null or
        /// the empty string.
        /// </param>
        /// <param name="arrayName">
        /// The name of the array. May be a general path expression, must
        /// not be null or the empty string. Each item in the array must
        /// be a simple string value.
        /// </param>
        /// <param name="separator">
        /// The string to be used to separate the items in the catenated
        /// string. Defaults to &quot;; &quot;, ASCII semicolon and space
        /// (U+003B, U+0020).
        /// </param>
        /// <param name="quotes">
        /// The characters to be used as quotes around array items that
        /// contain a separator. Defaults to &apos;&quot;&apos;
        /// </param>
        /// <param name="allowCommas">Option flag to control the catenation.</param>
        /// <returns>Returns the string containing the catenated array items.</returns>
        /// <exception cref="Com.Adobe.Xmp.XMPException">Forwards the Exceptions from the metadata processing</exception>
        public static string CatenateArrayItems(XMPMeta xmp, string schemaNS, string arrayName, string separator, string quotes, bool allowCommas)
        {
            ParameterAsserts.AssertSchemaNS(schemaNS);
            ParameterAsserts.AssertArrayName(arrayName);
            ParameterAsserts.AssertImplementation(xmp);
            if (separator == null || separator.Length == 0)
            {
                separator = "; ";
            }
            if (quotes == null || quotes.Length == 0)
            {
                quotes = "\"";
            }
            XMPMetaImpl xmpImpl = (XMPMetaImpl)xmp;
            XMPNode arrayNode = null;
            XMPNode currItem = null;
            // Return an empty result if the array does not exist,
            // hurl if it isn't the right form.
            XMPPath arrayPath = XMPPathParser.ExpandXPath(schemaNS, arrayName);
            arrayNode = XMPNodeUtils.FindNode(xmpImpl.GetRoot(), arrayPath, false, null);
            if (arrayNode == null)
            {
                return string.Empty;
            }
            else
            {
                if (!arrayNode.GetOptions().IsArray() || arrayNode.GetOptions().IsArrayAlternate())
                {
                    throw new XMPException("Named property must be non-alternate array", XMPErrorConstants.Badparam);
                }
            }
            // Make sure the separator is OK.
            CheckSeparator(separator);
            // Make sure the open and close quotes are a legitimate pair.
            char openQuote = quotes[0];
            char closeQuote = CheckQuotes(quotes, openQuote);
            // Build the result, quoting the array items, adding separators.
            // Hurl if any item isn't simple.
            StringBuilder catinatedString = new StringBuilder();
            for (Iterator it = arrayNode.IterateChildren(); it.HasNext(); )
            {
                currItem = (XMPNode)it.Next();
                if (currItem.GetOptions().IsCompositeProperty())
                {
                    throw new XMPException("Array items must be simple", XMPErrorConstants.Badparam);
                }
                string str = ApplyQuotes(currItem.GetValue(), openQuote, closeQuote, allowCommas);
                catinatedString.Append(str);
                if (it.HasNext())
                {
                    catinatedString.Append(separator);
                }
            }
            return catinatedString.ToString();
        }

        /// <summary>
        /// see
        /// <see cref="Com.Adobe.Xmp.XMPUtils.SeparateArrayItems(Com.Adobe.Xmp.XMPMeta, string, string, string, Com.Adobe.Xmp.Options.PropertyOptions, bool)"/>
        /// </summary>
        /// <param name="xmp">The XMP object containing the array to be updated.</param>
        /// <param name="schemaNS">
        /// The schema namespace URI for the array. Must not be null or
        /// the empty string.
        /// </param>
        /// <param name="arrayName">
        /// The name of the array. May be a general path expression, must
        /// not be null or the empty string. Each item in the array must
        /// be a simple string value.
        /// </param>
        /// <param name="catedStr">The string to be separated into the array items.</param>
        /// <param name="arrayOptions">Option flags to control the separation.</param>
        /// <param name="preserveCommas">Flag if commas shall be preserved</param>
        /// <exception cref="Com.Adobe.Xmp.XMPException">Forwards the Exceptions from the metadata processing</exception>
        public static void SeparateArrayItems(XMPMeta xmp, string schemaNS, string arrayName, string catedStr, PropertyOptions arrayOptions, bool preserveCommas)
        {
            ParameterAsserts.AssertSchemaNS(schemaNS);
            ParameterAsserts.AssertArrayName(arrayName);
            if (catedStr == null)
            {
                throw new XMPException("Parameter must not be null", XMPErrorConstants.Badparam);
            }
            ParameterAsserts.AssertImplementation(xmp);
            XMPMetaImpl xmpImpl = (XMPMetaImpl)xmp;
            // Keep a zero value, has special meaning below.
            XMPNode arrayNode = SeparateFindCreateArray(schemaNS, arrayName, arrayOptions, xmpImpl);
            // Extract the item values one at a time, until the whole input string is done.
            string itemValue;
            int itemStart;
            int itemEnd;
            int nextKind = UckNormal;
            int charKind = UckNormal;
            char ch = (char)0;
            char nextChar = (char)0;
            itemEnd = 0;
            int endPos = catedStr.Length;
            while (itemEnd < endPos)
            {
                // Skip any leading spaces and separation characters. Always skip commas here.
                // They can be kept when within a value, but not when alone between values.
                for (itemStart = itemEnd; itemStart < endPos; itemStart++)
                {
                    ch = catedStr[itemStart];
                    charKind = ClassifyCharacter(ch);
                    if (charKind == UckNormal || charKind == UckQuote)
                    {
                        break;
                    }
                }
                if (itemStart >= endPos)
                {
                    break;
                }
                if (charKind != UckQuote)
                {
                    // This is not a quoted value. Scan for the end, create an array
                    // item from the substring.
                    for (itemEnd = itemStart; itemEnd < endPos; itemEnd++)
                    {
                        ch = catedStr[itemEnd];
                        charKind = ClassifyCharacter(ch);
                        if (charKind == UckNormal || charKind == UckQuote || (charKind == UckComma && preserveCommas))
                        {
                            continue;
                        }
                        else
                        {
                            if (charKind != UckSpace)
                            {
                                break;
                            }
                            else
                            {
                                if ((itemEnd + 1) < endPos)
                                {
                                    ch = catedStr[itemEnd + 1];
                                    nextKind = ClassifyCharacter(ch);
                                    if (nextKind == UckNormal || nextKind == UckQuote || (nextKind == UckComma && preserveCommas))
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                        // Anything left?
                        break;
                    }
                    // Have multiple spaces, or a space followed by a
                    // separator.
                    itemValue = Runtime.Substring(catedStr, itemStart, itemEnd);
                }
                else
                {
                    // Accumulate quoted values into a local string, undoubling
                    // internal quotes that
                    // match the surrounding quotes. Do not undouble "unmatching"
                    // quotes.
                    char openQuote = ch;
                    char closeQuote = GetClosingQuote(openQuote);
                    itemStart++;
                    // Skip the opening quote;
                    itemValue = string.Empty;
                    for (itemEnd = itemStart; itemEnd < endPos; itemEnd++)
                    {
                        ch = catedStr[itemEnd];
                        charKind = ClassifyCharacter(ch);
                        if (charKind != UckQuote || !IsSurroundingQuote(ch, openQuote, closeQuote))
                        {
                            // This is not a matching quote, just append it to the
                            // item value.
                            itemValue += ch;
                        }
                        else
                        {
                            // This is a "matching" quote. Is it doubled, or the
                            // final closing quote?
                            // Tolerate various edge cases like undoubled opening
                            // (non-closing) quotes,
                            // or end of input.
                            if ((itemEnd + 1) < endPos)
                            {
                                nextChar = catedStr[itemEnd + 1];
                                nextKind = ClassifyCharacter(nextChar);
                            }
                            else
                            {
                                nextKind = UckSemicolon;
                                nextChar = (char)0x3B;
                            }
                            if (ch == nextChar)
                            {
                                // This is doubled, copy it and skip the double.
                                itemValue += ch;
                                // Loop will add in charSize.
                                itemEnd++;
                            }
                            else
                            {
                                if (!IsClosingingQuote(ch, openQuote, closeQuote))
                                {
                                    // This is an undoubled, non-closing quote, copy it.
                                    itemValue += ch;
                                }
                                else
                                {
                                    // This is an undoubled closing quote, skip it and
                                    // exit the loop.
                                    itemEnd++;
                                    break;
                                }
                            }
                        }
                    }
                }
                // Add the separated item to the array.
                // Keep a matching old value in case it had separators.
                int foundIndex = -1;
                for (int oldChild = 1; oldChild <= arrayNode.GetChildrenLength(); oldChild++)
                {
                    if (itemValue.Equals(arrayNode.GetChild(oldChild).GetValue()))
                    {
                        foundIndex = oldChild;
                        break;
                    }
                }
                XMPNode newItem = null;
                if (foundIndex < 0)
                {
                    newItem = new XMPNode(XMPConstConstants.ArrayItemName, itemValue, null);
                    arrayNode.AddChild(newItem);
                }
            }
        }

        /// <summary>Utility to find or create the array used by <code>separateArrayItems()</code>.</summary>
        /// <param name="schemaNS">a the namespace fo the array</param>
        /// <param name="arrayName">the name of the array</param>
        /// <param name="arrayOptions">the options for the array if newly created</param>
        /// <param name="xmp">the xmp object</param>
        /// <returns>Returns the array node.</returns>
        /// <exception cref="Com.Adobe.Xmp.XMPException">Forwards exceptions</exception>
        private static XMPNode SeparateFindCreateArray(string schemaNS, string arrayName, PropertyOptions arrayOptions, XMPMetaImpl xmp)
        {
            arrayOptions = XMPNodeUtils.VerifySetOptions(arrayOptions, null);
            if (!arrayOptions.IsOnlyArrayOptions())
            {
                throw new XMPException("Options can only provide array form", XMPErrorConstants.Badoptions);
            }
            // Find the array node, make sure it is OK. Move the current children
            // aside, to be readded later if kept.
            XMPPath arrayPath = XMPPathParser.ExpandXPath(schemaNS, arrayName);
            XMPNode arrayNode = XMPNodeUtils.FindNode(xmp.GetRoot(), arrayPath, false, null);
            if (arrayNode != null)
            {
                // The array exists, make sure the form is compatible. Zero
                // arrayForm means take what exists.
                PropertyOptions arrayForm = arrayNode.GetOptions();
                if (!arrayForm.IsArray() || arrayForm.IsArrayAlternate())
                {
                    throw new XMPException("Named property must be non-alternate array", XMPErrorConstants.Badxpath);
                }
                if (arrayOptions.EqualArrayTypes(arrayForm))
                {
                    throw new XMPException("Mismatch of specified and existing array form", XMPErrorConstants.Badxpath);
                }
            }
            else
            {
                // *** Right error?
                // The array does not exist, try to create it.
                // don't modify the options handed into the method
                arrayNode = XMPNodeUtils.FindNode(xmp.GetRoot(), arrayPath, true, arrayOptions.SetArray(true));
                if (arrayNode == null)
                {
                    throw new XMPException("Failed to create named array", XMPErrorConstants.Badxpath);
                }
            }
            return arrayNode;
        }

        /// <seealso cref="Com.Adobe.Xmp.XMPUtils.RemoveProperties(Com.Adobe.Xmp.XMPMeta, string, string, bool, bool)"/>
        /// <param name="xmp">The XMP object containing the properties to be removed.</param>
        /// <param name="schemaNS">
        /// Optional schema namespace URI for the properties to be
        /// removed.
        /// </param>
        /// <param name="propName">Optional path expression for the property to be removed.</param>
        /// <param name="doAllProperties">
        /// Option flag to control the deletion: do internal properties in
        /// addition to external properties.
        /// </param>
        /// <param name="includeAliases">
        /// Option flag to control the deletion: Include aliases in the
        /// "named schema" case above.
        /// </param>
        /// <exception cref="Com.Adobe.Xmp.XMPException">If metadata processing fails</exception>
        public static void RemoveProperties(XMPMeta xmp, string schemaNS, string propName, bool doAllProperties, bool includeAliases)
        {
            ParameterAsserts.AssertImplementation(xmp);
            XMPMetaImpl xmpImpl = (XMPMetaImpl)xmp;
            if (propName != null && propName.Length > 0)
            {
                // Remove just the one indicated property. This might be an alias,
                // the named schema might not actually exist. So don't lookup the
                // schema node.
                if (schemaNS == null || schemaNS.Length == 0)
                {
                    throw new XMPException("Property name requires schema namespace", XMPErrorConstants.Badparam);
                }
                XMPPath expPath = XMPPathParser.ExpandXPath(schemaNS, propName);
                XMPNode propNode = XMPNodeUtils.FindNode(xmpImpl.GetRoot(), expPath, false, null);
                if (propNode != null)
                {
                    if (doAllProperties || !Utils.IsInternalProperty(expPath.GetSegment(XMPPath.StepSchema).GetName(), expPath.GetSegment(XMPPath.StepRootProp).GetName()))
                    {
                        XMPNode parent = propNode.GetParent();
                        parent.RemoveChild(propNode);
                        if (parent.GetOptions().IsSchemaNode() && !parent.HasChildren())
                        {
                            // remove empty schema node
                            parent.GetParent().RemoveChild(parent);
                        }
                    }
                }
            }
            else
            {
                if (schemaNS != null && schemaNS.Length > 0)
                {
                    // Remove all properties from the named schema. Optionally include
                    // aliases, in which case
                    // there might not be an actual schema node.
                    // XMP_NodePtrPos schemaPos;
                    XMPNode schemaNode = XMPNodeUtils.FindSchemaNode(xmpImpl.GetRoot(), schemaNS, false);
                    if (schemaNode != null)
                    {
                        if (RemoveSchemaChildren(schemaNode, doAllProperties))
                        {
                            xmpImpl.GetRoot().RemoveChild(schemaNode);
                        }
                    }
                    if (includeAliases)
                    {
                        // We're removing the aliases also. Look them up by their
                        // namespace prefix.
                        // But that takes more code and the extra speed isn't worth it.
                        // Lookup the XMP node
                        // from the alias, to make sure the actual exists.
                        XMPAliasInfo[] aliases = XMPMetaFactory.GetSchemaRegistry().FindAliases(schemaNS);
                        for (int i = 0; i < aliases.Length; i++)
                        {
                            XMPAliasInfo info = aliases[i];
                            XMPPath path = XMPPathParser.ExpandXPath(info.GetNamespace(), info.GetPropName());
                            XMPNode actualProp = XMPNodeUtils.FindNode(xmpImpl.GetRoot(), path, false, null);
                            if (actualProp != null)
                            {
                                XMPNode parent = actualProp.GetParent();
                                parent.RemoveChild(actualProp);
                            }
                        }
                    }
                }
                else
                {
                    // Remove all appropriate properties from all schema. In this case
                    // we don't have to be
                    // concerned with aliases, they are handled implicitly from the
                    // actual properties.
                    for (Iterator it = xmpImpl.GetRoot().IterateChildren(); it.HasNext(); )
                    {
                        XMPNode schema = (XMPNode)it.Next();
                        if (RemoveSchemaChildren(schema, doAllProperties))
                        {
                            it.Remove();
                        }
                    }
                }
            }
        }

        /// <seealso cref="Com.Adobe.Xmp.XMPUtils.AppendProperties(Com.Adobe.Xmp.XMPMeta, Com.Adobe.Xmp.XMPMeta, bool, bool)"/>
        /// <param name="source">The source XMP object.</param>
        /// <param name="destination">The destination XMP object.</param>
        /// <param name="doAllProperties">Do internal properties in addition to external properties.</param>
        /// <param name="replaceOldValues">Replace the values of existing properties.</param>
        /// <param name="deleteEmptyValues">Delete destination values if source property is empty.</param>
        /// <exception cref="Com.Adobe.Xmp.XMPException">Forwards the Exceptions from the metadata processing</exception>
        public static void AppendProperties(XMPMeta source, XMPMeta destination, bool doAllProperties, bool replaceOldValues, bool deleteEmptyValues)
        {
            ParameterAsserts.AssertImplementation(source);
            ParameterAsserts.AssertImplementation(destination);
            XMPMetaImpl src = (XMPMetaImpl)source;
            XMPMetaImpl dest = (XMPMetaImpl)destination;
            for (Iterator it = src.GetRoot().IterateChildren(); it.HasNext(); )
            {
                XMPNode sourceSchema = (XMPNode)it.Next();
                // Make sure we have a destination schema node
                XMPNode destSchema = XMPNodeUtils.FindSchemaNode(dest.GetRoot(), sourceSchema.GetName(), false);
                bool createdSchema = false;
                if (destSchema == null)
                {
                    destSchema = new XMPNode(sourceSchema.GetName(), sourceSchema.GetValue(), new PropertyOptions().SetSchemaNode(true));
                    dest.GetRoot().AddChild(destSchema);
                    createdSchema = true;
                }
                // Process the source schema's children.
                for (Iterator ic = sourceSchema.IterateChildren(); ic.HasNext(); )
                {
                    XMPNode sourceProp = (XMPNode)ic.Next();
                    if (doAllProperties || !Utils.IsInternalProperty(sourceSchema.GetName(), sourceProp.GetName()))
                    {
                        AppendSubtree(dest, sourceProp, destSchema, replaceOldValues, deleteEmptyValues);
                    }
                }
                if (!destSchema.HasChildren() && (createdSchema || deleteEmptyValues))
                {
                    // Don't create an empty schema / remove empty schema.
                    dest.GetRoot().RemoveChild(destSchema);
                }
            }
        }

        /// <summary>
        /// Remove all schema children according to the flag
        /// <code>doAllProperties</code>.
        /// </summary>
        /// <remarks>
        /// Remove all schema children according to the flag
        /// <code>doAllProperties</code>. Empty schemas are automatically remove
        /// by <code>XMPNode</code>
        /// </remarks>
        /// <param name="schemaNode">a schema node</param>
        /// <param name="doAllProperties">flag if all properties or only externals shall be removed.</param>
        /// <returns>Returns true if the schema is empty after the operation.</returns>
        private static bool RemoveSchemaChildren(XMPNode schemaNode, bool doAllProperties)
        {
            for (Iterator it = schemaNode.IterateChildren(); it.HasNext(); )
            {
                XMPNode currProp = (XMPNode)it.Next();
                if (doAllProperties || !Utils.IsInternalProperty(schemaNode.GetName(), currProp.GetName()))
                {
                    it.Remove();
                }
            }
            return !schemaNode.HasChildren();
        }

        /// <seealso cref="AppendProperties(Com.Adobe.Xmp.XMPMeta, Com.Adobe.Xmp.XMPMeta, bool, bool, bool)"/>
        /// <param name="destXMP">The destination XMP object.</param>
        /// <param name="sourceNode">the source node</param>
        /// <param name="destParent">the parent of the destination node</param>
        /// <param name="replaceOldValues">Replace the values of existing properties.</param>
        /// <param name="deleteEmptyValues">
        /// flag if properties with empty values should be deleted
        /// in the destination object.
        /// </param>
        /// <exception cref="Com.Adobe.Xmp.XMPException"/>
        private static void AppendSubtree(XMPMetaImpl destXMP, XMPNode sourceNode, XMPNode destParent, bool replaceOldValues, bool deleteEmptyValues)
        {
            XMPNode destNode = XMPNodeUtils.FindChildNode(destParent, sourceNode.GetName(), false);
            bool valueIsEmpty = false;
            if (deleteEmptyValues)
            {
                valueIsEmpty = sourceNode.GetOptions().IsSimple() ? sourceNode.GetValue() == null || sourceNode.GetValue().Length == 0 : !sourceNode.HasChildren();
            }
            if (deleteEmptyValues && valueIsEmpty)
            {
                if (destNode != null)
                {
                    destParent.RemoveChild(destNode);
                }
            }
            else
            {
                if (destNode == null)
                {
                    // The one easy case, the destination does not exist.
                    destParent.AddChild((XMPNode)sourceNode.Clone());
                }
                else
                {
                    if (replaceOldValues)
                    {
                        // The destination exists and should be replaced.
                        destXMP.SetNode(destNode, sourceNode.GetValue(), sourceNode.GetOptions(), true);
                        destParent.RemoveChild(destNode);
                        destNode = (XMPNode)sourceNode.Clone();
                        destParent.AddChild(destNode);
                    }
                    else
                    {
                        // The destination exists and is not totally replaced. Structs and
                        // arrays are merged.
                        PropertyOptions sourceForm = sourceNode.GetOptions();
                        PropertyOptions destForm = destNode.GetOptions();
                        if (sourceForm != destForm)
                        {
                            return;
                        }
                        if (sourceForm.IsStruct())
                        {
                            // To merge a struct process the fields recursively. E.g. add simple missing fields.
                            // The recursive call to AppendSubtree will handle deletion for fields with empty
                            // values.
                            for (Iterator it = sourceNode.IterateChildren(); it.HasNext(); )
                            {
                                XMPNode sourceField = (XMPNode)it.Next();
                                AppendSubtree(destXMP, sourceField, destNode, replaceOldValues, deleteEmptyValues);
                                if (deleteEmptyValues && !destNode.HasChildren())
                                {
                                    destParent.RemoveChild(destNode);
                                }
                            }
                        }
                        else
                        {
                            if (sourceForm.IsArrayAltText())
                            {
                                // Merge AltText arrays by the "xml:lang" qualifiers. Make sure x-default is first.
                                // Make a special check for deletion of empty values. Meaningful in AltText arrays
                                // because the "xml:lang" qualifier provides unambiguous source/dest correspondence.
                                for (Iterator it = sourceNode.IterateChildren(); it.HasNext(); )
                                {
                                    XMPNode sourceItem = (XMPNode)it.Next();
                                    if (!sourceItem.HasQualifier() || !XMPConstConstants.XmlLang.Equals(sourceItem.GetQualifier(1).GetName()))
                                    {
                                        continue;
                                    }
                                    int destIndex = XMPNodeUtils.LookupLanguageItem(destNode, sourceItem.GetQualifier(1).GetValue());
                                    if (deleteEmptyValues && (sourceItem.GetValue() == null || sourceItem.GetValue().Length == 0))
                                    {
                                        if (destIndex != -1)
                                        {
                                            destNode.RemoveChild(destIndex);
                                            if (!destNode.HasChildren())
                                            {
                                                destParent.RemoveChild(destNode);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (destIndex == -1)
                                        {
                                            // Not replacing, keep the existing item.
                                            if (!XMPConstConstants.XDefault.Equals(sourceItem.GetQualifier(1).GetValue()) || !destNode.HasChildren())
                                            {
                                                sourceItem.CloneSubtree(destNode);
                                            }
                                            else
                                            {
                                                XMPNode destItem = new XMPNode(sourceItem.GetName(), sourceItem.GetValue(), sourceItem.GetOptions());
                                                sourceItem.CloneSubtree(destItem);
                                                destNode.AddChild(1, destItem);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (sourceForm.IsArray())
                                {
                                    // Merge other arrays by item values. Don't worry about order or duplicates. Source
                                    // items with empty values do not cause deletion, that conflicts horribly with
                                    // merging.
                                    for (Iterator @is = sourceNode.IterateChildren(); @is.HasNext(); )
                                    {
                                        XMPNode sourceItem = (XMPNode)@is.Next();
                                        bool match = false;
                                        for (Iterator id = destNode.IterateChildren(); id.HasNext(); )
                                        {
                                            XMPNode destItem = (XMPNode)id.Next();
                                            if (ItemValuesMatch(sourceItem, destItem))
                                            {
                                                match = true;
                                            }
                                        }
                                        if (!match)
                                        {
                                            destNode = (XMPNode)sourceItem.Clone();
                                            destParent.AddChild(destNode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Compares two nodes including its children and qualifier.</summary>
        /// <param name="leftNode">an <code>XMPNode</code></param>
        /// <param name="rightNode">an <code>XMPNode</code></param>
        /// <returns>Returns true if the nodes are equal, false otherwise.</returns>
        /// <exception cref="Com.Adobe.Xmp.XMPException">Forwards exceptions to the calling method.</exception>
        private static bool ItemValuesMatch(XMPNode leftNode, XMPNode rightNode)
        {
            PropertyOptions leftForm = leftNode.GetOptions();
            PropertyOptions rightForm = rightNode.GetOptions();
            if (leftForm.Equals(rightForm))
            {
                return false;
            }
            if (leftForm.GetOptions() == 0)
            {
                // Simple nodes, check the values and xml:lang qualifiers.
                if (!leftNode.GetValue().Equals(rightNode.GetValue()))
                {
                    return false;
                }
                if (leftNode.GetOptions().GetHasLanguage() != rightNode.GetOptions().GetHasLanguage())
                {
                    return false;
                }
                if (leftNode.GetOptions().GetHasLanguage() && !leftNode.GetQualifier(1).GetValue().Equals(rightNode.GetQualifier(1).GetValue()))
                {
                    return false;
                }
            }
            else
            {
                if (leftForm.IsStruct())
                {
                    // Struct nodes, see if all fields match, ignoring order.
                    if (leftNode.GetChildrenLength() != rightNode.GetChildrenLength())
                    {
                        return false;
                    }
                    for (Iterator it = leftNode.IterateChildren(); it.HasNext(); )
                    {
                        XMPNode leftField = (XMPNode)it.Next();
                        XMPNode rightField = XMPNodeUtils.FindChildNode(rightNode, leftField.GetName(), false);
                        if (rightField == null || !ItemValuesMatch(leftField, rightField))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    // Array nodes, see if the "leftNode" values are present in the
                    // "rightNode", ignoring order, duplicates,
                    // and extra values in the rightNode-> The rightNode is the
                    // destination for AppendProperties.
                    Debug.Assert(leftForm.IsArray());
                    for (Iterator il = leftNode.IterateChildren(); il.HasNext(); )
                    {
                        XMPNode leftItem = (XMPNode)il.Next();
                        bool match = false;
                        for (Iterator ir = rightNode.IterateChildren(); ir.HasNext(); )
                        {
                            XMPNode rightItem = (XMPNode)ir.Next();
                            if (ItemValuesMatch(leftItem, rightItem))
                            {
                                match = true;
                                break;
                            }
                        }
                        if (!match)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        // All of the checks passed.
        /// <summary>Make sure the separator is OK.</summary>
        /// <remarks>
        /// Make sure the separator is OK. It must be one semicolon surrounded by
        /// zero or more spaces. Any of the recognized semicolons or spaces are
        /// allowed.
        /// </remarks>
        /// <param name="separator"/>
        /// <exception cref="Com.Adobe.Xmp.XMPException"/>
        private static void CheckSeparator(string separator)
        {
            bool haveSemicolon = false;
            for (int i = 0; i < separator.Length; i++)
            {
                int charKind = ClassifyCharacter(separator[i]);
                if (charKind == UckSemicolon)
                {
                    if (haveSemicolon)
                    {
                        throw new XMPException("Separator can have only one semicolon", XMPErrorConstants.Badparam);
                    }
                    haveSemicolon = true;
                }
                else
                {
                    if (charKind != UckSpace)
                    {
                        throw new XMPException("Separator can have only spaces and one semicolon", XMPErrorConstants.Badparam);
                    }
                }
            }
            if (!haveSemicolon)
            {
                throw new XMPException("Separator must have one semicolon", XMPErrorConstants.Badparam);
            }
        }

        /// <summary>
        /// Make sure the open and close quotes are a legitimate pair and return the
        /// correct closing quote or an exception.
        /// </summary>
        /// <param name="quotes">opened and closing quote in a string</param>
        /// <param name="openQuote">the open quote</param>
        /// <returns>Returns a corresponding closing quote.</returns>
        /// <exception cref="Com.Adobe.Xmp.XMPException"/>
        private static char CheckQuotes(string quotes, char openQuote)
        {
            char closeQuote;
            int charKind = ClassifyCharacter(openQuote);
            if (charKind != UckQuote)
            {
                throw new XMPException("Invalid quoting character", XMPErrorConstants.Badparam);
            }
            if (quotes.Length == 1)
            {
                closeQuote = openQuote;
            }
            else
            {
                closeQuote = quotes[1];
                charKind = ClassifyCharacter(closeQuote);
                if (charKind != UckQuote)
                {
                    throw new XMPException("Invalid quoting character", XMPErrorConstants.Badparam);
                }
            }
            if (closeQuote != GetClosingQuote(openQuote))
            {
                throw new XMPException("Mismatched quote pair", XMPErrorConstants.Badparam);
            }
            return closeQuote;
        }

        /// <summary>
        /// Classifies the character into normal chars, spaces, semicola, quotes,
        /// control chars.
        /// </summary>
        /// <param name="ch">a char</param>
        /// <returns>Return the character kind.</returns>
        private static int ClassifyCharacter(char ch)
        {
            if (Spaces.IndexOf(ch) >= 0 || (unchecked((int)(0x2000)) <= ch && ch <= unchecked((int)(0x200B))))
            {
                return UckSpace;
            }
            else
            {
                if (Commas.IndexOf(ch) >= 0)
                {
                    return UckComma;
                }
                else
                {
                    if (Semicola.IndexOf(ch) >= 0)
                    {
                        return UckSemicolon;
                    }
                    else
                    {
                        if (Quotes.IndexOf(ch) >= 0 || (unchecked((int)(0x3008)) <= ch && ch <= unchecked((int)(0x300F))) || (unchecked((int)(0x2018)) <= ch && ch <= unchecked((int)(0x201F))))
                        {
                            return UckQuote;
                        }
                        else
                        {
                            if (ch < unchecked((int)(0x0020)) || Controls.IndexOf(ch) >= 0)
                            {
                                return UckControl;
                            }
                            else
                            {
                                // Assume typical case.
                                return UckNormal;
                            }
                        }
                    }
                }
            }
        }

        /// <param name="openQuote">the open quote char</param>
        /// <returns>Returns the matching closing quote for an open quote.</returns>
        private static char GetClosingQuote(char openQuote)
        {
            switch (openQuote)
            {
                case (char)0x0022:
                {
                    return (char)0x0022;
                }

                case (char)0x00AB:
                {
                    // ! U+0022 is both opening and closing.
                    //        Not interpreted as brackets anymore
                    //        case 0x005B:
                    //            return 0x005D;
                    return (char)0x00BB;
                }

                case (char)0x00BB:
                {
                    // ! U+00AB and U+00BB are reversible.
                    return (char)0x00AB;
                }

                case (char)0x2015:
                {
                    return (char)0x2015;
                }

                case (char)0x2018:
                {
                    // ! U+2015 is both opening and closing.
                    return (char)0x2019;
                }

                case (char)0x201A:
                {
                    return (char)0x201B;
                }

                case (char)0x201C:
                {
                    return (char)0x201D;
                }

                case (char)0x201E:
                {
                    return (char)0x201F;
                }

                case (char)0x2039:
                {
                    return (char)0x203A;
                }

                case (char)0x203A:
                {
                    // ! U+2039 and U+203A are reversible.
                    return (char)0x2039;
                }

                case (char)0x3008:
                {
                    return (char)0x3009;
                }

                case (char)0x300A:
                {
                    return (char)0x300B;
                }

                case (char)0x300C:
                {
                    return (char)0x300D;
                }

                case (char)0x300E:
                {
                    return (char)0x300F;
                }

                case (char)0x301D:
                {
                    return (char)0x301F;
                }

                default:
                {
                    // ! U+301E also closes U+301D.
                    return (char)0;
                }
            }
        }

        /// <summary>Add quotes to the item.</summary>
        /// <param name="item">the array item</param>
        /// <param name="openQuote">the open quote character</param>
        /// <param name="closeQuote">the closing quote character</param>
        /// <param name="allowCommas">flag if commas are allowed</param>
        /// <returns>Returns the value in quotes.</returns>
        private static string ApplyQuotes(string item, char openQuote, char closeQuote, bool allowCommas)
        {
            if (item == null)
            {
                item = string.Empty;
            }
            bool prevSpace = false;
            int charOffset;
            int charKind;
            // See if there are any separators in the value. Stop at the first
            // occurrance. This is a bit
            // tricky in order to make typical typing work conveniently. The purpose
            // of applying quotes
            // is to preserve the values when splitting them back apart. That is
            // CatenateContainerItems
            // and SeparateContainerItems must round trip properly. For the most
            // part we only look for
            // separators here. Internal quotes, as in -- Irving "Bud" Jones --
            // won't cause problems in
            // the separation. An initial quote will though, it will make the value
            // look quoted.
            int i;
            for (i = 0; i < item.Length; i++)
            {
                char ch = item[i];
                charKind = ClassifyCharacter(ch);
                if (i == 0 && charKind == UckQuote)
                {
                    break;
                }
                if (charKind == UckSpace)
                {
                    // Multiple spaces are a separator.
                    if (prevSpace)
                    {
                        break;
                    }
                    prevSpace = true;
                }
                else
                {
                    prevSpace = false;
                    if ((charKind == UckSemicolon || charKind == UckControl) || (charKind == UckComma && !allowCommas))
                    {
                        break;
                    }
                }
            }
            if (i < item.Length)
            {
                // Create a quoted copy, doubling any internal quotes that match the
                // outer ones. Internal quotes did not stop the "needs quoting"
                // search, but they do need
                // doubling. So we have to rescan the front of the string for
                // quotes. Handle the special
                // case of U+301D being closed by either U+301E or U+301F.
                StringBuilder newItem = new StringBuilder(item.Length + 2);
                int splitPoint;
                for (splitPoint = 0; splitPoint <= i; splitPoint++)
                {
                    if (ClassifyCharacter(item[i]) == UckQuote)
                    {
                        break;
                    }
                }
                // Copy the leading "normal" portion.
                newItem.Append(openQuote).Append(Runtime.Substring(item, 0, splitPoint));
                for (charOffset = splitPoint; charOffset < item.Length; charOffset++)
                {
                    newItem.Append(item[charOffset]);
                    if (ClassifyCharacter(item[charOffset]) == UckQuote && IsSurroundingQuote(item[charOffset], openQuote, closeQuote))
                    {
                        newItem.Append(item[charOffset]);
                    }
                }
                newItem.Append(closeQuote);
                item = newItem.ToString();
            }
            return item;
        }

        /// <param name="ch">a character</param>
        /// <param name="openQuote">the opening quote char</param>
        /// <param name="closeQuote">the closing quote char</param>
        /// <returns>Return it the character is a surrounding quote.</returns>
        private static bool IsSurroundingQuote(char ch, char openQuote, char closeQuote)
        {
            return ch == openQuote || IsClosingingQuote(ch, openQuote, closeQuote);
        }

        /// <param name="ch">a character</param>
        /// <param name="openQuote">the opening quote char</param>
        /// <param name="closeQuote">the closing quote char</param>
        /// <returns>Returns true if the character is a closing quote.</returns>
        private static bool IsClosingingQuote(char ch, char openQuote, char closeQuote)
        {
            return ch == closeQuote || (openQuote == unchecked((int)(0x301D)) && ch == unchecked((int)(0x301E)) || ch == unchecked((int)(0x301F)));
        }

        /// <summary>
        /// U+0022 ASCII space<br />
        /// U+3000, ideographic space<br />
        /// U+303F, ideographic half fill space<br />
        /// U+2000..U+200B, en quad through zero width space
        /// </summary>
        private const string Spaces = "\u0020\u3000\u303F";

        /// <summary>
        /// U+002C, ASCII comma<br />
        /// U+FF0C, full width comma<br />
        /// U+FF64, half width ideographic comma<br />
        /// U+FE50, small comma<br />
        /// U+FE51, small ideographic comma<br />
        /// U+3001, ideographic comma<br />
        /// U+060C, Arabic comma<br />
        /// U+055D, Armenian comma
        /// </summary>
        private const string Commas = "\u002C\uFF0C\uFF64\uFE50\uFE51\u3001\u060C\u055D";

        /// <summary>
        /// U+003B, ASCII semicolon<br />
        /// U+FF1B, full width semicolon<br />
        /// U+FE54, small semicolon<br />
        /// U+061B, Arabic semicolon<br />
        /// U+037E, Greek "semicolon" (really a question mark)
        /// </summary>
        private const string Semicola = "\u003B\uFF1B\uFE54\u061B\u037E";

        /// <summary>
        /// U+0022 ASCII quote<br />
        /// The square brackets are not interpreted as quotes anymore (bug #2674672)
        /// (ASCII '[' (0x5B) and ']' (0x5D) are used as quotes in Chinese and
        /// Korean.)<br />
        /// U+00AB and U+00BB, guillemet quotes<br />
        /// U+3008..U+300F, various quotes.<br />
        /// U+301D..U+301F, double prime quotes.<br />
        /// U+2015, dash quote.<br />
        /// U+2018..U+201F, various quotes.<br />
        /// U+2039 and U+203A, guillemet quotes.
        /// </summary>
        private const string Quotes = "\"\u00AB\u00BB\u301D\u301E\u301F\u2015\u2039\u203A";

        /// <summary>
        /// U+0000..U+001F ASCII controls<br />
        /// U+2028, line separator.<br />
        /// U+2029, paragraph separator.
        /// </summary>
        private const string Controls = "\u2028\u2029";
        // "\"\u005B\u005D\u00AB\u00BB\u301D\u301E\u301F\u2015\u2039\u203A";
    }
}
