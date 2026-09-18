bl_info = {
    "name": "Big Pickle AI Chat",
    "author": "BARAKI",
    "version": (1, 0, 0),
    "blender": (3, 6, 0),
    "location": "View3D > Sidebar > AI",
    "description": "Chat with Claude AI directly in Blender",
    "category": "Development",
}

import bpy
import json
import urllib.request
import urllib.error
import ssl
import threading
import re
from bpy.props import (
    StringProperty, CollectionProperty, IntProperty,
    BoolProperty, EnumProperty, PointerProperty
)
from bpy.types import Operator, Panel, PropertyGroup

# ─── Properties ─────────────────────────────────────────────────────
class AI_ChatMessage(PropertyGroup):
    role: StringProperty(name="Role", default="user")
    content: StringProperty(name="Content", default="")
    is_code: BoolProperty(name="Is Code", default=False)
    timestamp: StringProperty(name="Timestamp", default="")


class AI_ChatProperties(PropertyGroup):
    messages: CollectionProperty(type=AI_ChatMessage)
    message_index: IntProperty(default=-1)
    input_text: StringProperty(name="Message", default="")
    api_key: StringProperty(name="API Key", default="", subtype='PASSWORD')
    is_generating: BoolProperty(name="Generating", default=False)
    system_prompt: StringProperty(
        name="System Prompt",
        default=(
            "You are a Blender AI assistant. You write Python scripts for Blender. "
            "When user asks you to create or modify something, write the exact bpy code. "
            "Always wrap code in ```python``` blocks. "
            "Keep responses concise. Execute code using bpy.ops and bpy.data APIs."
        )
    )
    auto_execute: BoolProperty(
        name="Auto-execute code",
        description="Automatically execute generated Python code",
        default=False
    )
    show_system_prompt: BoolProperty(
        name="Show System Prompt",
        default=False
    )
    model: EnumProperty(
        name="Model",
        items=[
            ('claude-opus-4-20250514', 'Claude Opus 4', 'Most capable'),
            ('claude-sonnet-4-20250514', 'Claude Sonnet 4', 'Balanced'),
            ('claude-3-5-sonnet-20241022', 'Claude 3.5 Sonnet', 'Fast & smart'),
        ],
        default='claude-sonnet-4-20250514'
    )
    max_tokens: IntProperty(
        name="Max Tokens",
        default=4096,
        min=256,
        max=16384
    )
    temperature: bpy.props.FloatProperty(
        name="Temperature",
        default=0.7,
        min=0.0,
        max=1.0
    )
    chat_history_visible: BoolProperty(
        name="Show Chat History",
        default=True
    )


# ─── Claude API ──────────────────────────────────────────────────────
def call_claude_api(api_key, messages, system_prompt, model,
                    max_tokens=4096, temperature=0.7):
    """Call Claude API and return response text."""
    url = "https://api.anthropic.com/v1/messages"

    # Build messages array (skip system prompt, it goes separately)
    api_messages = []
    for msg in messages:
        api_messages.append({
            "role": msg.role,
            "content": msg.content
        })

    payload = {
        "model": model,
        "max_tokens": max_tokens,
        "temperature": temperature,
        "system": system_prompt,
        "messages": api_messages
    }

    headers = {
        "Content-Type": "application/json",
        "x-api-key": api_key,
        "anthropic-version": "2023-06-01"
    }

    ctx = ssl.create_default_context()
    data = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(url, data=data, headers=headers, method='POST')

    try:
        with urllib.request.urlopen(req, context=ctx, timeout=120) as resp:
            result = json.loads(resp.read().decode('utf-8'))

        # Extract text from response
        text = ""
        for block in result.get("content", []):
            if block.get("type") == "text":
                text += block.get("text", "")

        return text, None
    except urllib.error.HTTPError as e:
        body = e.read().decode('utf-8') if e.fp else str(e)
        return None, f"API Error {e.code}: {body}"
    except Exception as e:
        return None, str(e)


def extract_code_blocks(text):
    """Extract Python code blocks from markdown."""
    pattern = r'```(?:python)?\s*\n(.*?)```'
    matches = re.findall(pattern, text, re.DOTALL)
    return matches


# ─── Operators ──────────────────────────────────────────────────────
class AI_OT_SendMessage(Operator):
    bl_idname = "ai_chat.send_message"
    bl_label = "Send Message"
    bl_description = "Send message to Claude AI"

    def execute(self, context):
        props = context.scene.ai_chat
        message = props.input_text.strip()

        if not message:
            self.report({'WARNING'}, "Type a message first")
            return {'CANCELLED'}

        if not props.api_key:
            self.report({'ERROR'}, "Set your Anthropic API key first!")
            return {'CANCELLED'}

        # Add user message
        user_msg = props.messages.add()
        user_msg.role = "user"
        user_msg.content = message
        user_msg.is_code = False
        props.input_text = ""

        # Show context info
        context_info = self._get_scene_context()
        if context_info:
            user_msg.content += f"\n\n[Blender context: {context_info}]"

        # Call API in thread
        props.is_generating = True
        thread = threading.Thread(
            target=self._call_api,
            args=(context, props)
        )
        thread.daemon = True
        thread.start()

        return {'FINISHED'}

    def _get_scene_context(self):
        """Get current scene context for AI."""
        try:
            scene = bpy.context.scene
            obj = bpy.context.active_object
            parts = [f"Scene: {scene.name}"]

            if obj:
                parts.append(f"Active object: {obj.name} ({obj.type})")
                if obj.type == 'MESH':
                    mesh = obj.data
                    parts.append(
                        f"Mesh: {len(mesh.polygons)} faces, "
                        f"{len(mesh.vertices)} vertices"
                    )

            parts.append(f"Selected: {len(bpy.context.selected_objects)} objects")
            parts.append(f"Frame: {scene.frame_current}/{scene.frame_end}")
            parts.append(f"Render engine: {scene.render.engine}")

            return " | ".join(parts)
        except:
            return ""

    def _call_api(self, context, props):
        """Call API in background thread."""
        try:
            # Prepare messages (exclude the last user message with context)
            messages = []
            for i, msg in enumerate(props.messages):
                if i == len(props.messages) - 1:
                    # Last message - send without context suffix
                    clean_msg = props.messages[-1].content.split("\n\n[Blender context:")[0]
                    messages.append({"role": msg.role, "content": clean_msg})
                else:
                    messages.append({"role": msg.role, "content": msg.content})

            response, error = call_claude_api(
                api_key=props.api_key,
                messages=messages,
                system_prompt=props.system_prompt,
                model=props.model,
                max_tokens=props.max_tokens,
                temperature=props.temperature
            )

            def update_ui():
                props.is_generating = False
                if error:
                    err_msg = props.messages.add()
                    err_msg.role = "assistant"
                    err_msg.content = f"Error: {error}"
                    self.report({'ERROR'}, error)
                else:
                    ai_msg = props.messages.add()
                    ai_msg.role = "assistant"
                    ai_msg.content = response
                    ai_msg.is_code = bool(extract_code_blocks(response))

                    # Auto-execute if enabled
                    if props.auto_execute:
                        code_blocks = extract_code_blocks(response)
                        for code in code_blocks:
                            try:
                                exec(code)
                            except Exception as e:
                                print(f"[AI Chat] Execute error: {e}")

            # Update UI on main thread
            bpy.app.timers.register(update_ui, first_interval=0.1)

        except Exception as e:
            def show_error():
                props.is_generating = False
                err_msg = props.messages.add()
                err_msg.role = "assistant"
                err_msg.content = f"Error: {str(e)}"
            bpy.app.timers.register(show_error, first_interval=0.1)


class AI_OT_ExecuteCode(Operator):
    bl_idname = "ai_chat.execute_code"
    bl_label = "Execute Code"
    bl_description = "Execute the last AI-generated code"

    code_text: StringProperty()

    def execute(self, context):
        try:
            exec(self.code_text)
            self.report({'INFO'}, "Code executed successfully!")
        except Exception as e:
            self.report({'ERROR'}, f"Error: {str(e)}")
        return {'FINISHED'}


class AI_OT_CopyCode(Operator):
    bl_idname = "ai_chat.copy_code"
    bl_label = "Copy Code"

    code_text: StringProperty()

    def execute(self, context):
        context.window_manager.clipboard = self.code_text
        self.report({'INFO'}, "Code copied to clipboard")
        return {'FINISHED'}


class AI_OT_ClearChat(Operator):
    bl_idname = "ai_chat.clear_chat"
    bl_label = "Clear Chat"
    bl_description = "Clear all messages"

    def execute(self, context):
        context.scene.ai_chat.messages.clear()
        return {'FINISHED'}


class AI_OT_StopGenerating(Operator):
    bl_idname = "ai_chat.stop_generating"
    bl_label = "Stop"
    bl_description = "Stop generating"

    def execute(self, context):
        context.scene.ai_chat.is_generating = False
        return {'FINISHED'}


class AI_OT_PresetPrompt(Operator):
    bl_idname = "ai_chat.preset_prompt"
    bl_label = "Use Preset"

    prompt_text: StringProperty()

    def execute(self, context):
        context.scene.ai_chat.input_text = self.prompt_text
        return {'FINISHED'}


class AI_OT_DeleteMessage(Operator):
    bl_idname = "ai_chat.delete_message"
    bl_label = "Delete Message"

    index: IntProperty()

    def execute(self, context):
        context.scene.ai_chat.messages.remove(self.index)
        return {'FINISHED'}


# ─── UI Panels ──────────────────────────────────────────────────────
class AI_CHAT_PT_MainPanel(Panel):
    bl_label = "Big Pickle AI"
    bl_idname = "AI_CHAT_PT_main"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_chat

        # API Key
        box = layout.box()
        row = box.row(align=True)
        row.label(text="API Key:", icon='KEY')
        row.prop(props, "api_key", text="")

        if not props.api_key:
            box.label(text="Get key at: console.anthropic.com", icon='INFO')

        # Model selection
        row = box.row(align=True)
        row.prop(props, "model", text="")

        # Settings
        row = box.row(align=True)
        row.prop(props, "max_tokens", text="Tokens")
        row.prop(props, "temperature", text="Temp")


class AI_CHAT_PT_ChatPanel(Panel):
    bl_label = "Chat"
    bl_idname = "AI_CHAT_PT_chat"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_CHAT_PT_main"

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_chat

        # Chat messages
        box = layout.box()

        if len(props.messages) == 0:
            box.label(text="Start a conversation!", icon='INFO')
        else:
            for i, msg in enumerate(props.messages):
                if msg.role == "user":
                    row = box.row(align=True)
                    row.label(text=f"You:", icon='USER')
                    row.operator("ai_chat.delete_message", text="", icon='X', emboss=False).index = i

                    # User message
                    sub = box.box()
                    sub.label(text=msg.content[:200])
                    if len(msg.content) > 200:
                        sub.label(text="...")
                else:
                    row = box.row(align=True)
                    row.label(text="AI:", icon='COMM')
                    row.operator("ai_chat.delete_message", text="", icon='X', emboss=False).index = i

                    # AI message - parse code blocks
                    parts = msg.content.split('```')
                    for j, part in enumerate(parts):
                        if j % 2 == 0:
                            # Text part
                            if part.strip():
                                sub = box.box()
                                lines = part.strip().split('\n')
                                for line in lines[:10]:
                                    sub.label(text=line)
                                if len(lines) > 10:
                                    sub.label(text=f"... ({len(lines)} lines)")
                        else:
                            # Code part
                            code = part.strip()
                            if code.startswith('python'):
                                code = code[6:].strip()

                            sub = box.box()
                            sub.label(text="Code:", icon='SCRIPT')

                            # Code display (truncated)
                            code_lines = code.split('\n')
                            for line in code_lines[:8]:
                                sub.label(text=line)
                            if len(code_lines) > 8:
                                sub.label(text=f"... ({len(code_lines)} lines)")

                            # Action buttons
                            row = sub.row(align=True)
                            op = row.operator("ai_chat.execute_code",
                                            text="Execute", icon='PLAY')
                            op.code_text = code
                            op = row.operator("ai_chat.copy_code",
                                            text="Copy", icon='COPYDOWN')
                            op.code_text = code

        # Separator
        layout.separator()

        # Input area
        box = layout.box()
        box.label(text="Your message:", icon='TEXT')
        box.prop(props, "input_text", text="")

        # Send button
        row = box.row(align=True)
        if props.is_generating:
            row.operator("ai_chat.stop_generating", text="Stop", icon='PAUSE')
            row.label(text="Generating...", icon='TIME')
        else:
            row.operator("ai_chat.send_message", text="Send", icon='PLAY')

        row.prop(props, "auto_execute", text="Auto-exec", toggle=True)


class AI_CHAT_PT_PresetsPanel(Panel):
    bl_label = "Quick Prompts"
    bl_idname = "AI_CHAT_PT_presets"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_CHAT_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    def draw(self, context):
        layout = self.layout

        presets = [
            ("Create a low-poly tree", "Create a low-poly tree with a trunk and foliage"),
            ("Medieval house", "Create a simple medieval house with walls, roof and door"),
            ("Car model", "Create a basic car body with wheels"),
            ("Sci-fi corridor", "Create a sci-fi corridor with glowing panels"),
            ("Sword model", "Create a medieval sword with handle and blade"),
            ("Landscape", "Create a terrain with mountains and a river"),
            ("Character base", "Create a simple humanoid character base mesh"),
            ("Furniture", "Create a modern table and chair set"),
            ("Spaceship", "Create a simple spaceship with wings and engines"),
            ("Weapon rack", "Create a weapon rack with swords and shields"),
            ("Treasure chest", "Create a treasure chest with metal bands"),
            ("Potion bottles", "Create 3 different potion bottles"),
            ("Tree forest", "Create a forest with 20 trees of different sizes"),
            ("Castle wall", "Create a castle wall with battlements and tower"),
            ("Spaceship interior", "Create the interior of a spaceship cockpit"),
            ("Gem stones", "Create 5 different colored gemstones"),
            ("Robot", "Create a simple robot with body, head and arms"),
            ("Rock formation", "Create a natural rock formation"),
            ("Skull", "Create a human skull"),
            ("Chest armor", "Create a chest plate armor"),
        ]

        for label, prompt in presets:
            op = layout.operator("ai_chat.preset_prompt",
                               text=label,
                               icon='FORWARD')
            op.prompt_text = prompt


class AI_CHAT_PT_SettingsPanel(Panel):
    bl_label = "Settings"
    bl_idname = "AI_CHAT_PT_settings"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_CHAT_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_chat

        box = layout.box()
        box.label(text="System Prompt:", icon='TEXT')
        box.prop(props, "system_prompt", text="")

        row = layout.row()
        row.operator("ai_chat.clear_chat", text="Clear All Messages", icon='TRASH')


# ─── Registration ────────────────────────────────────────────────────
classes = (
    AI_ChatMessage,
    AI_ChatProperties,
    AI_OT_SendMessage,
    AI_OT_ExecuteCode,
    AI_OT_CopyCode,
    AI_OT_ClearChat,
    AI_OT_StopGenerating,
    AI_OT_PresetPrompt,
    AI_OT_DeleteMessage,
    AI_CHAT_PT_MainPanel,
    AI_CHAT_PT_ChatPanel,
    AI_CHAT_PT_PresetsPanel,
    AI_CHAT_PT_SettingsPanel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.ai_chat = PointerProperty(type=AI_ChatProperties)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.ai_chat


if __name__ == "__main__":
    register()
