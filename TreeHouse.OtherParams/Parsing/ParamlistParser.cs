using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TreeHouse.Common;
using TreeHouse.OtherParams.Model;

namespace TreeHouse.OtherParams.Parsing;

public class ParamlistParser
{
    private static readonly Regex dataVer = new(@"^data_ver\s+(?<ver>\d+)$");
    private static readonly Regex defaultClass = new(@"^default(?<what>\w+)\s+(?<class>\w+)$");
    private static readonly Regex table = new(@"^table\s+(?<id>\d+)\s+(?<name>\w+)$");
    private static readonly Regex clazz = new(@"^class\s+(?<name>\w+)\s+(?<attribute>\w+)\s+(?<value>.+)$");
    private static readonly Regex paramDecl = new(@"^paramid\s+(?<class>\w+)\.(?<param>\w+)\s+(?<id>\d+)$");
    private static readonly Regex help = new(@"^help\s+(?<class>\w+)\.(?<param>\w+)\s+""(?<help>.*)""$");
    private static readonly Regex paramDef = new(@"^(?<class>[\w\d]+)\.(?<param>[\w\d]+)\s+type\s+(?<type>[\w\d]+)(?:,\s+default\s+(?<default>(?:"".*?"")|(?:[^\s,]+)))?(?:,\s+priority\s+(?<priority>[\d\.]+))?(?:,\s+tg\s+(?<tg>[\d\.]+))?\s+(?:flag\s+(?<flag>\w+),\s+)*(?:editType\s+(?<editType>[\w\d_]+),\s+)?(?:group\s+""(?<group>.*?)"",\s+)?(?:engineBindingName\s+""(?<engineBindingName>.*?)"",\s+)?constraintParam\s+""(?<constraintParam>.*?)""$");

    private static readonly List<(Regex regex, Action<ParamlistParser, Match> parser)> LineParseList = new()
    {
        (dataVer, (x, m) => x.ParseDataVer(m)),
        (defaultClass, (x, m) => x.ParseDefault(m)),
        (table, (x, m) => x.ParseTable(m)),
        (clazz, (x, m) => x.ParseClass(m)),
        (paramDecl, (x, m) => x.ParseParamDecl(m)),
        (help, (x, m) => x.ParseHelp(m)),
        (paramDef, (x, m) => x.ParseParamDef(m))
    };

    private readonly Dictionary<string, Table> tables = new();
    private readonly Dictionary<string, Class> classes = new();

    private int dataVerValue = 0;

#pragma warning disable S2933 // Fields that are only assigned in the constructor should be "readonly"
    private string? defaultClientAvatarClass = null;
    private string? defaultPartyClass = null;
    private string? defaultTradeClass = null;
    private string? defaultMailClass = null;
    private string? defaultClanClass = null;
#pragma warning restore S2933

    Class? lastClass = null;
    ParamDeclaration? lastParamDecl = null;
    string? lastParamDeclName = null;
    Param? lastParam = null;
    int paramId = 1;

    bool haveParsed = false;

    public async Task ReadParamlistAsync(TextReader reader)
    {
        if (haveParsed)
            throw new InvalidOperationException("Can only parse once");
        haveParsed = true;

        int lineCount = 1;

        
        await foreach (string line in reader.ReadAllLinesAsync())
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                continue;

            try
            {
                ParseLine(line);
            }
            catch (FormatException e)
            {
                throw new ParseException(line, lineCount, "Failed to parse input line", e);
            }

            lineCount++;
        }

        CheckParseResults();
    } 

    public async Task WriteDbAsync(ParamDb db)
    {
        await db.Tables.AddRangeAsync(tables.Values);
        await db.Classes.AddRangeAsync(classes.Values);

        await db.SetGlobalsAsync(new Globals()
        {
            DataVer = dataVerValue,
            DefaultClanClass = classes[defaultClanClass!],
            DefaultClientAvatarClass = classes[defaultClientAvatarClass!],
            DefaultMailClass = classes[defaultMailClass!],
            DefaultPartyClass = classes[defaultPartyClass!],
            DefaultTradeClass = classes[defaultTradeClass!]
        });
    }

    private void CheckParseResults()
    {
        foreach (var item in classes.Values)
        {
            if (item.ContentTableBinding == null)
                throw new FormatException($"Class {item.Name} is missing it's ContentTableBinding attribute");

            foreach (var param in item.DeclaredParams)
            {
                if (param.Definition == null)
                    throw new FormatException($"Class {param.Class.Name} has a parameter that was declared, but not defined");
            }
        }
    }

    private void ParseLine(string line)
    {
        foreach (var (regex, parser) in LineParseList)
        {
            if (regex.TryMatch(line, out Match match))
            {
                parser(this, match);
                return;
            }
        }

        throw new FormatException("Bad syntax");
    }

    private void ParseDataVer(Match match) => dataVerValue = int.Parse(match.Groups["ver"].Value);

    private void ParseDefault(Match match) => DefaultField(match.Groups["what"].Value) = match.Groups["class"].Value;

    private ref string? DefaultField(string name)
    {
        switch (name)
        {
            case "ClientAvatarClass": return ref defaultClientAvatarClass;
            case "PartyClass": return ref defaultPartyClass;
            case "TradeClass": return ref defaultTradeClass;
            case "MailClass": return ref defaultMailClass;
            case "ClanClass": return ref defaultClanClass;
            default: throw new FormatException("Unknown default declaration name: " + name);
        }
    }

    private void ParseTable(Match match)
    {
        string name = match.Groups["name"].Value;
        tables.Add(name, new Table() { Id = int.Parse(match.Groups["id"].Value) + 1, Name = name });
    }

    private void ParseClass(Match match)
    {
        string name = match.Groups["name"].Value;

        if (lastClass == null || lastClass.Name != name)
        {
            lastClass = new Class() { Name = name, ContentTableBinding = null! };
            classes.Add(name, lastClass);
        }

        string value = match.Groups["value"].Value;
        switch (match.Groups["attribute"].Value)
        {
            case "uniqueid": lastClass.UniqueId = int.Parse(value); break;
            case "bindsTo": lastClass.BindsTo = value; break;
            case "contentTableBinding": lastClass.ContentTableBinding = tables[value]; break;
            case "icon": lastClass.Icon = value.Trim('\"'); break;
            case "extends": lastClass.Extends = classes[value]; break;
            default: throw new FormatException("Unknown class attribute: " + name);
        }
    }

    private void ParseParamDecl(Match match)
    {
        if (lastParamDecl != null && lastParamDecl.Definition == null)
            throw new FormatException($"Parameter {lastParamDeclName} on class {lastParamDecl.Class.Name} was declared, but not defined");

        if (lastClass == null || match.Groups["class"].Value != lastClass.Name)
            throw new FormatException("Parameter declarations should follow the declaration of their parent class");

        string name = match.Groups["param"].Value;

        Param? definition = null;

        Class? ownerClass = lastClass.Extends;
        while (ownerClass != null)
        {
            definition = ownerClass.DefinedParams.FirstOrDefault(x => x.Name == name);
            if (definition != null)
                break;
            ownerClass = ownerClass.Extends;
        }

        lastParamDecl = new ParamDeclaration() { Class = lastClass, Definition = definition!, ParamId = int.Parse(match.Groups["id"].Value) };
        lastParamDeclName = name;
        lastClass.DeclaredParams.Add(lastParamDecl);
    }

    private void ParseHelp(Match match)
    {
        if (lastClass == null || lastParam == null || match.Groups["class"].Value != lastClass.Name || match.Groups["param"].Value != lastParam.Name)
            throw new FormatException("Help lines should follow their corresponding parameter definition");
        lastParam.Help = match.Groups["help"].Value;
    }

    private void ParseParamDef(Match match)
    {
        // TODO: What to do with over-definitions?

        if (lastClass == null || lastParamDecl == null || match.Groups["class"].Value != lastClass.Name || match.Groups["param"].Value != lastParamDeclName)
            throw new FormatException("Parameter definitions should follow their corresponding declaration");

        lastParam = new Param()
        {
            Id = paramId++,
            Name = match.Groups["param"].Value,
            DefinedIn = lastClass,
            Type = match.Groups["type"].Value,
            Default = match.Groups["default"].ValueIfSuccess()?.Trim('\"'),
            EditType = match.Groups["editType"].ValueIfSuccess(),
            EngineBindingName = match.Groups["engineBindingName"].ValueIfSuccess(),
            ConstraintParam = match.Groups["constraintParam"].Value,
        };

        string? priority = match.Groups["priority"].ValueIfSuccess();
        if (priority != null)
            lastParam.Priority = float.Parse(priority);

        string? tg = match.Groups["tg"].ValueIfSuccess();
        if (tg != null)
            lastParam.Tg = float.Parse(tg);

        foreach (string flag in match.Groups["flag"].Captures.Select(x => x.Value))
        {
            switch (flag)
            {
                case "persistent": lastParam.Persistent = true; break;
                case "content": lastParam.Content = true; break;
                case "perInstanceSetting": lastParam.PerInstanceSetting = true; break;
                case "nodeOwn": lastParam.NodeOwn = true; break;
                case "deprecated": lastParam.Deprecated = true; break;
                case "serverOwn": lastParam.ServerOwn = true; break;
                case "excludeFromClient": lastParam.ExcludeFromClient = true; break;
                case "dupeSetOk": lastParam.DupeSetOk = true; break;
                case "clientUnknown": lastParam.ClientUnknown = true; break;
                case "metric": lastParam.Metric = true; break;
                case "clientOwn": lastParam.ClientUnknown = true; break;
                case "equipSlot": lastParam.EquipSlot = true; break;
                case "clientPrivileged": lastParam.ClientPrivileged = true; break;
                case "uts": lastParam.Uts = true; break;
                case "clientInit": lastParam.ClientInit = true; break;
                default: throw new FormatException("Unknown parameter flag: " + flag);
            }
        }

        lastClass.DefinedParams.Add(lastParam);

        lastParamDecl.Definition = lastParam;
        lastParamDecl = null;
    }
}
