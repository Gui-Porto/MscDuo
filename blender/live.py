import os
import runpy
import traceback

import bpy

FOLDER = os.path.dirname(os.path.abspath(__file__))
SCENE = os.path.join(FOLDER, "cena.py")
RELEVO = os.path.join(os.path.expanduser("~"), "Documents", "MySummerCar", "Mods", "Config", "Mod Settings", "MscDuo", "dumps", "relevo.obj")
state = {"mtime": 0.0}


def redraw():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            area.tag_redraw()


def tick():
    try:
        mtime = max(os.path.getmtime(p) for p in (SCENE, RELEVO) if os.path.exists(p))
        if mtime != state["mtime"]:
            state["mtime"] = mtime
            runpy.run_path(SCENE, run_name="__main__")
            redraw()
            print("[MSC Duo] cena atualizada")
    except Exception:
        traceback.print_exc()
    return 1.0


if not bpy.app.timers.is_registered(tick):
    bpy.app.timers.register(tick, persistent=True)
print("[MSC Duo] modo ao vivo ligado, observando", SCENE)
