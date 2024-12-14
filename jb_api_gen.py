import builtins
import json
import os
import sys
from dataclasses import dataclass, field
from io import TextIOWrapper
from pathlib import Path
from typing import Any

# There's probably an existing tool that can do some of this stuff (as in JSON -> C# struct)
# But I enjoy reinventing the wheel (aka I like writing scripts)
# Also this script is a mess because it was done in like five different parts


def _capitalize_first(s: str) -> str:
    if len(s) == 0:
        return s
    return s[0].upper() + s[1:]


SubfieldData = dict[str, "FieldData"]


@dataclass
class DataSet:
    send_data: set[frozenset[str]] = field(default_factory=set)
    room_data: SubfieldData = field(default_factory=dict)
    player_data: SubfieldData = field(default_factory=dict)
    enum_data: dict[str, set[str]] = field(default_factory=dict)


class FieldData:
    def __init__(self, name: str) -> None:
        self._name = name
        self._types: set[type] = set()
        self._subdata: SubfieldData = {}
        self._can_be_none: bool = False

    def update(self, val: Any) -> None:
        if val is None:
            self._can_be_none = True
            return

        next_type = type(val)
        self._types.add(next_type)

        if next_type is list and len(val) > 0:
            if "entry" not in self._subdata:
                self._subdata["entry"] = self.__class__("entry")
            self._subdata["entry"].update(val[0])
        elif next_type is dict and len(val) > 0:
            for key, value in val.items():
                if key not in self._subdata:
                    self._subdata[key] = self.__class__(key)
                self._subdata[key].update(value)

    def print(self, indent_level: int = 0) -> None:
        header = f'    {"".ljust(indent_level)}{self._name} ->'
        if len(self._subdata) == 0:  # basic type(s) only
            field_str = ""
            for entry in self._types:
                field_str += f"{entry.__name__} "
            if self._can_be_none:
                field_str += "NULL"
            print(f"{header} {field_str}")
        elif len(self._types) == 1 and next(iter(self._types)) == list:  # handle list manually
            inner_type = next(iter(self._subdata["entry"]._types))
            list_typing = inner_type.__name__ if "entry" in self._subdata else "unknown"
            if len(self._subdata["entry"]._types) > 1:
                print('{header} WARNING: Unhandled type "list of complex type"')
            elif inner_type is list:
                print('{header} WARNING: Unhandled type "list of lists"')
            elif inner_type is dict:
                print(f'{header} list{" (can be NULL)" if self._can_be_none else ""}:')
                self._subdata["entry"].print(indent_level + 4)
            else:
                print(f'{header} list[{list_typing}]{" NULL" if self._can_be_none else ""}')
        elif len(self._types) == 1 and next(iter(self._types)) == dict:  # recurse
            print(f'{header} object{" (can be NULL)" if self._can_be_none else ""}:')
            for field in self._subdata.values():
                field.print(indent_level + 4)
        else:  # complicated
            field_str = ""
            for entry in self._types:
                field_str += f"{entry.__name__} "
            if self._can_be_none:
                field_str += "NULL"
            print(f"{header} Complex Type ({field_str})")

    def _python_type_to_cs_type(self, t: type) -> str:
        match t:
            case builtins.str:
                return "string"
            case builtins.int:
                return "int"
            case builtins.float:
                return "double"
            case builtins.bool:
                return "bool"
            case _:
                return "UNHANDLED"

    def _determine_typing(self, enum_names: set[str], specifier: str) -> tuple[str, str, str, SubfieldData]:
        followup_name: str = None
        followup_obj: SubfieldData = None
        type_name = "UNKNOWN"
        comment = ""

        if self._name in enum_names:  # Enum type
            type_name = _capitalize_first(self._name)
        elif len(self._types) == 0:  # Type that is always null, add comment about it
            type_name = "JRaw"
            comment = " // Type is unknown because the value was always null in API data"
        elif len(self._types) == 1:  # Only one type, easy
            if len(self._subdata) == 0:
                this_type = next(iter(self._types))
                if this_type == list or this_type == dict:
                    type_name = "JRaw"
                    comment = " // Always empty list in API data"
                else:
                    type_name = self._python_type_to_cs_type(this_type)
            elif next(iter(self._types)) == list:
                inner_type = next(iter(self._subdata["entry"]._types))
                if len(self._types) == 0:
                    type_name = "JRaw"
                    comment = " // Always empty in API data"
                elif len(self._subdata["entry"]._types) > 1:
                    type_name = "JRaw"
                    comment = ' // WARNING: Unhandled type "list of complex type"'
                elif inner_type is list:
                    type_name = "JRaw"
                    comment = ' // WARNING: Unhandled type "list of lists"'
                elif inner_type is dict:
                    followup_name = f"{specifier}{_capitalize_first(self._name)}"
                    followup_obj = self._subdata["entry"]._subdata
                    type_name = f"List<{followup_name}>"
                else:
                    type_name = f'List<{self._python_type_to_cs_type(next(iter(self._subdata["entry"]._types)))}>'
            elif next(iter(self._types)) == dict:
                followup_name = f"{specifier}{_capitalize_first(self._name)}"
                followup_obj = self._subdata
                type_name = followup_name
        else:  # More than one type, complicated
            type_name = "JRaw"
            comment = " // Can be multiple types: "
            for entry in self._types:
                if entry == list:
                    comment += f"List or "
                elif entry == dict:
                    comment += f"Object or "
                else:
                    comment += f"{self._python_type_to_cs_type(entry)} or "
            comment = comment[:-4]
            if self._can_be_none:
                comment += " (or null)"

        return type_name, comment, (followup_name, followup_obj)

    def print_cs(
        self, file: TextIOWrapper, enum_names: set[str], specifier: str, indent: str
    ) -> tuple[str, SubfieldData]:
        # Some fields come in as ints and floats at different times, treat such fields as floats
        if len(self._types) > 1 and all(t == int or t == float for t in self._types):
            self._types.clear()
            self._types.add(float)

        # Determine type, extra notes, and if there's a follow up
        type_name, comment, followup = self._determine_typing(enum_names, specifier)

        # Mark as nullable if needed
        if type_name != "JRaw" and self._can_be_none:
            type_name += "?"

        # Assumed to already be inside the class
        file.write(f'\n{indent}[JsonProperty("{self._name}")]\n')
        file.write(f"{indent}public {type_name} {_capitalize_first(self._name)} {{ get; set; }}{comment}\n")

        # Return sub object for future processing
        return followup


def _output_template_files(game_name: str, src_folder_path: str, using_bc: bool) -> None:
    client_class = "BcSerializedClient" if using_bc else "PlayerSerializedClient"

    # Folder will always exist since it was created earlier
    client = Path(f"{src_folder_path}/Games/{game_name}/{game_name}Client.cs")
    if not client.exists():
        with open(client, "w") as file:
            file.write(
                f"""using JackboxGPT.Games.Common;
using JackboxGPT.Games.{game_name}.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Games.{game_name};

public class {game_name}Client : {client_class}<{game_name}Room, {game_name}Player>
{{
    public {game_name}Client(IConfigurationProvider configuration, ILogger logger, int instance)
        : base(configuration, logger, instance)
    {{
    }}
}}"""
            )
    else:
        print(f"Skipping Client template creation as {game_name}Client.cs already exists")

    # If this folder doesn't exist something is wrong anyway
    engine = Path(f"{src_folder_path}/Engines/{game_name}Engine.cs")
    if not engine.exists():
        with open(engine, "w") as file:
            file.write(
                f"""using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.{game_name};
using JackboxGPT.Games.{game_name}.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Engines;

public class {game_name}Engine : BaseJackboxEngine<{game_name}Client>
{{
    protected override string Tag => "TODO";

    public {game_name}Engine(ICompletionService completionService, ILogger logger, {game_name}Client client, int instance)
        : base(completionService, logger, client, instance)
    {{
        JackboxClient.OnSelfUpdate += OnSelfUpdate;
        JackboxClient.OnRoomUpdate += OnRoomUpdate;
        JackboxClient.Connect();
    }}

    private void OnSelfUpdate(object sender, Revision<{game_name}Player> revision)
    {{
    }}

    private void OnRoomUpdate(object sender, Revision<{game_name}Room> revision)
    {{
    }}
}}"""
            )
    else:
        print(f"Skipping Engine template creation as {game_name}Engine.cs already exists")


def _handle_enum(file: TextIOWrapper, enum_data: dict[str, set[str]], enums_todo: list[str], indent: str) -> None:
    def _getEnumTrueName(input: str) -> str:
        if "-" not in input:
            return _capitalize_first(input)
        parts = input.split("-")
        return "".join(_capitalize_first(part) for part in parts)

    for name, vals in enum_data.items():
        if len(vals) == 0 or name not in enums_todo:
            continue

        # Add json converter line if needed for any values within
        has_blank_default = any(len(name) == 0 for name in vals)
        if has_blank_default or any("-" in name for name in vals):
            file.write("\n[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]")
        file.write(f"\npublic enum {_capitalize_first(name)}\n{{\n")

        # Sometimes enum values come in as blank, treat those as default
        if any(len(name) == 0 for name in vals):
            file.write(f'{indent}[System.Runtime.Serialization.EnumMember(Value = "")]\n')
        file.write(f"{indent}None")  # All enums have 'None' as their default value

        for val in sorted(vals):
            if len(val) == 0:
                continue
            true_val = _getEnumTrueName(val)
            prepend = f",\n{indent}"
            if "-" in val:  # Hyphens aren't allowed in enum names so this value needs a special serializer note
                prepend = f'{prepend}[System.Runtime.Serialization.EnumMember(Value = "{val}")]\n{indent}'
            file.write(f"{prepend}{true_val}")
        file.write("\n}\n")


def _has_key_deep_check(data: SubfieldData, key: str) -> bool:
    if data is None:
        return False
    if key in data:
        return True
    for element in data.values():
        if _has_key_deep_check(element._subdata, key):
            return True
    return False


def _handle_file_output(
    file: TextIOWrapper,
    base_name: str,
    struct_data: SubfieldData,
    enums_remaining: list[str],
    category: str,
    top_level: bool = False,
    indent: str = "    ",
) -> bool:
    class_name = f"{base_name}{category}" if top_level else base_name
    standard_header = f"""// This file was generated with jb_api_gen.py

#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JackboxGPT.Games.{base_name}.Models;
"""
    empty_header = f"""// This file was generated with jb_api_gen.py

namespace JackboxGPT.Games.{base_name}.Models;

public struct {class_name}
{{
}}"""

    if top_level:
        # Return early if file is empty
        if len(struct_data) == 0:
            file.write(empty_header)
            return False

        # Determine which enums should be placed in this file
        enums_todo = []
        for idx in range(len(enums_remaining) - 1, -1, -1):
            if _has_key_deep_check(struct_data, enums_remaining[idx]):
                enums_todo.append(enums_remaining.pop(idx))

        # Write header and enum blocks
        file.write(standard_header)
        _handle_enum(file, data.enum_data, enums_todo, indent)

    enums = data.enum_data.keys()
    followups: list[tuple[str, SubfieldData]] = []
    file.write(f"\npublic struct {class_name}\n{{")
    for field in struct_data.values():
        re = field.print_cs(file, enums, category, indent)
        if re[1] is not None:
            followups.append(re)
    file.write("}\n")

    for name, field in followups:
        _handle_file_output(file, name, field, [], name)

    return True


def _output_as_cs(data: DataSet, game_name: str, src_folder_path: str, using_bc: bool) -> None:
    # Create folder(s)
    model_folder = Path(f"{src_folder_path}/Games/{game_name}/Models")
    model_folder.mkdir(parents=True, exist_ok=True)

    # Track which enums haven't been written yet
    enums_remaining = list(data.enum_data.keys())

    # Write out Player file
    with open(f"{model_folder}/{game_name}Player.cs", "w") as file:
        wrote_out = _handle_file_output(file, game_name, data.player_data, enums_remaining, "Player", top_level=True)

    # Write out Room file
    with open(f"{model_folder}/{game_name}Room.cs", "w") as file:
        wrote_out |= _handle_file_output(file, game_name, data.room_data, enums_remaining, "Room", top_level=True)

    # Write out Client/Engine template files (if the previous steps seemed to work)
    if wrote_out:
        _output_template_files(game_name, src_folder_path, using_bc)
    else:
        print("Player and Room structures are both empty, please check input data")


def _print_results(data: DataSet, verbose: bool = False) -> None:
    if verbose:
        print(f"\nRoom Fields:")
        for field in data.room_data.values():
            field.print()

        print("\nPlayer Fields:")
        for field in data.player_data.values():
            field.print()

        print("\nEnums:")
        for enum, val_set in data.enum_data.items():
            print(f"    {enum} values: {val_set}")

    for entry in data.send_data:
        print("\nSent = {")
        for field in sorted(entry):
            print(f"  {field}")
        print("}")


# This is a slow way to do this but whatever
# For the record, this is here so that json can be extracted while ignoring
# other logging artifacts (things like "[loader] load success" or "D_0e_jtX.js:12:238")
def _transform_to_json(lines: list[str], required: str = None) -> list[dict]:
    bracket_index: int = 0
    cur_str: str = ""

    def _sort_char(c: str) -> bool:
        nonlocal bracket_index
        nonlocal cur_str

        if c == "{":
            bracket_index += 1
            cur_str += c
        elif c == "}" and bracket_index > 0:
            bracket_index -= 1
            cur_str += c
            return bracket_index == 0
        elif bracket_index > 0:
            cur_str += c
        return False

    json_msgs: list[dict] = []
    for line in lines:
        for c in line:
            if _sort_char(c):
                try:
                    msg = json.loads(cur_str)
                    if required is None or required in msg:
                        json_msgs.append(msg)
                except ValueError:
                    pass
                finally:
                    cur_str = ""

    return json_msgs


def _track_receive_data(msg_data: dict, data: SubfieldData) -> None:
    for field_name, field_val in msg_data.items():
        if field_name not in data:
            data[field_name] = FieldData(field_name)
        data[field_name].update(field_val)


def _track_send_data(msg_data: dict[str, Any], data: set[frozenset[str]], enum_keys: list[str]) -> None:
    def _get_val_rep(key: str, val: Any) -> str:
        # Enum Keyed Field
        if key in enum_keys:
            try:
                # Prefixed Key
                return f'"{key}": "{val[:val.index(":")+1]}<ID>"'
            except ValueError:
                # Non-Prefixed Key
                return f'"{key}": "{val}"'

        # Non-Key Multi-Field
        if type(val) == dict:
            str_rep = f'"{key}": {{\n'
            for subkey, subval in val.items():
                str_rep += f"    {_get_val_rep(subkey, subval)}\n"
            str_rep += "  }"
            return str_rep

        # Non-Key Single-Field
        return f'"{key}": <{type(val).__name__}>'

    data.add(frozenset(_get_val_rep(field_name, field_val) for field_name, field_val in msg_data.items()))


def _sort_object_by_key(data: DataSet, key: str) -> bool:
    # Handle prefix keys
    if key == "bc:room" or key == "room":
        _track_receive_data(msg["result"]["val"], data.room_data)
        return True
    elif key.startswith("bc:customer:") or key.startswith("player:"):
        _track_receive_data(msg["result"]["val"], data.player_data)
        return True
    elif ":" in key:  # Newer games have lots of action:<ID> messages, but they're not needed as far as I've seen
        return False

    # Handle standard keys
    match key:
        case "textDescriptions":
            pass
        case "connectedPlayers":
            pass
        case "roundInfo":
            pass
        case "horseRaceInfo":
            pass
        case _:
            print(f'WARNING: Unhandled object key {msg["result"]["key"]}')

    return False


if __name__ == "__main__":
    # Make sure script is called correctly
    if len(sys.argv) < 3:
        print("Usage: jb_api_gen.py <game_name> <input_folder> [enum_name...]")
        exit(1)

    # Make sure script is run in the expected location
    src_dir = Path(__file__).resolve().parent / "src"
    if not (src_dir / "Engines").is_dir():
        print("ERROR: this script is intended to be run inside the JackboxGPT repo folder")
        exit(1)

    # Fetch all the input data
    lines: list[str] = []
    for path in [f for f in os.listdir(sys.argv[2]) if f.endswith(".txt") or f.endswith(".json")]:
        with open(f"{sys.argv[2]}/{path}", encoding="utf-8") as file:
            lines.extend(file.readlines())

    # Prepare the data set, including setting up sets for each enum given
    data = DataSet()
    if len(sys.argv) > 4:
        for enum in sys.argv[3:]:
            data.enum_data[enum] = set()

    # Fetch JSON messages from input data
    json_msgs: list[dict] = _transform_to_json(lines, "opcode")

    # Handle messages based on opcode
    using_bc: bool = None
    action_keys = ["action", "key"]
    for msg in json_msgs:
        match msg["opcode"]:
            case "object":
                # Determine what kind of messages are being exchanged
                if _sort_object_by_key(data, msg["result"]["key"]) and using_bc is None:
                    using_bc = msg["result"]["key"].startswith("bc:")

                # Keep track of enum values
                for key in data.enum_data.keys():
                    if key in msg["result"]["val"] and type(msg["result"]["val"][key]) == str:
                        data.enum_data[key].add(msg["result"]["val"][key])
            case "client/send":
                _track_send_data(msg["params"]["body"], data.send_data, action_keys)
            case "text/update":
                _track_send_data(msg["params"], data.send_data, action_keys)
            case "object/update":
                _track_send_data(msg["params"], data.send_data, action_keys)
            case "client/welcome":
                pass
            case "room/lock":
                pass
            case "room/exit":
                pass
            case "ok":
                pass
            case "text":
                pass
            case "drop":
                pass
            case _:
                print(f'WARNING: Unhandled opcode key {msg["opcode"]}')

    # Output C# files and print sent message breakdown
    _output_as_cs(data, sys.argv[1], src_dir, using_bc)
    _print_results(data)
