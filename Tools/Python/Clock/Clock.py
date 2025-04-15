#!/usr/bin/env python3
import tkinter as tk
import time
import sys

SET_TRANSPARENT = False

# Hide Windows command prompt window (doesn't affect macOS/Linux)
if sys.platform == 'win32':
    import ctypes
    ctypes.windll.user32.ShowWindow(ctypes.windll.kernel32.GetConsoleWindow(), 0)

class ElegantClock:
    def __init__(self, root):
        self.root = root
        self.root.title("Elegant Clock")
        self.root.attributes("-topmost", True)

        # Morandi color palette scheme
        self.colors = {
            "background": "#F5F5F5",    # Light gray base
            "foreground": "#598BAF",    # Mist blue
            "highlight": "#A8C3BC"     # Slate green
        }
        self.root.configure(bg=self.colors["background"])
        self._init_window()

        # Main clock display
        self.time_label = tk.Label(
            root,
            font=('Helvetica', 48),
            bg=self.colors["background"],
            fg=self.colors["foreground"]
        )
        self.time_label.pack(expand=True, fill='both', padx=20, pady=10)

        # Window interaction setup
        self.setup_drag()
        self.update_time()

    def _init_window(self, width=300, heigh=120):
        self.root.overrideredirect(True)
        if SET_TRANSPARENT:
            self.root.attributes("-transparentcolor", self.colors["background"])
        else:
            self.root.attributes("-alpha", 0.85)  # 85% opacity

        screen_width = self.root.winfo_screenwidth()
        screen_height = self.root.winfo_screenheight()
        x = (screen_width - width) // 2
        y = (screen_height - heigh) // 2
        self.root.geometry(f"{width}x{heigh}+{x}+{y}")

    def setup_drag(self):
        """Configure window dragging functionality"""
        self.root.bind("<ButtonPress-1>", self.start_move)
        self.root.bind("<ButtonRelease-1>", self.stop_move)
        self.root.bind("<B1-Motion>", self.on_move)

    def update_time(self):
        """Update time display"""
        current_time = time.strftime("%H:%M:%S")
        self.time_label.config(text=current_time)
        self.root.after(200, self.update_time)  # 200ms refresh cycle

    def start_move(self, event):
        """Begin window dragging"""
        self._start_x = event.x
        self._start_y = event.y

    def stop_move(self, event):
        """End window dragging"""
        self._start_x = None
        self._start_y = None

    def on_move(self, event):
        """Handle window movement"""
        dx = event.x - self._start_x
        dy = event.y - self._start_y
        x = self.root.winfo_x() + dx
        y = self.root.winfo_y() + dy
        self.root.geometry(f"+{x}+{y}")

if __name__ == "__main__":
    root = tk.Tk()
    app = ElegantClock(root)
    root.mainloop()
