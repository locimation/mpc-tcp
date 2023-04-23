# mpc-tcp

This SIMPL# program allows MPC button panels (MPC3-302, MPC3-201, etc) to be used as peripherals of a 3rd-party control system by hosting a TCP API.

CPZ files can be downloaded from [https://github.com/locimation/mpc-tcp/releases](https://github.com/locimation/mpc-tcp/releases).

## TCP API

The TCP server listens on port 9023.

Commands sent to the server are terminated by a newline (`\n`).

Responses are terminated by a double newline (`\n\n`).


### Commands

Enable the button panel (including backlight and beep)
```
ENABLE
```

Disable the button panel (including backlight and beep)
```
DISABLE
```

Configure button as momentary
```
CONFIGURE Button1 MOMENTARY
```

Configure button as toggle
```
CONFIGURE Button7 TOGGLE
```

Configure buttons in interlock groups
```
CONFIGURE Button1 INTERLOCK 1
CONFIGURE Button2 INTERLOCK 1
CONFIGURE Button3 INTERLOCK 2
CONFIGURE Button4 INTERLOCK 2
```

Set button state
```
SET Button1 TRUE
```

Set interlock group state
```
SETGRROUP 1 Button2
```

Set volume level
```
SETVOLUME 65535
```

Poll for current state
```
POLL
```


### Feedback

Volume change
```
UPDATE:
  VOLUME = 65535
```

Button state change
```
UPDATE
  BUTTON<Button1> = True
```

Group state change
```
UPDATE
  GROUP<1> = True
```
