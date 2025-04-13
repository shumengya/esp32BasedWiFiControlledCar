import network
import socket
from machine import Pin, PWM
import time
import websocket
import _thread

# WiFi配置
SSID = 'shumengya'
PASSWORD = '0123456789'
WS_PORT = 8080

# 引脚定义
MOTOR_A1 = PWM(Pin(18))  # 使用PWM控制
MOTOR_A2 = PWM(Pin(19))
MOTOR_B1 = PWM(Pin(21))
MOTOR_B2 = PWM(Pin(22))

BRUSH = Pin(13, Pin.OUT)
BAG = Pin(14, Pin.OUT)

LED2 = Pin(2,Pin.OUT)

# PWM配置
PWM_FREQ = 50  # 50Hz
for motor in [MOTOR_A1, MOTOR_A2, MOTOR_B1, MOTOR_B2]:
    motor.freq(PWM_FREQ)

# 全局变量
# 0-100%
pwm1 = 60  
pwm2 = 60
current_flag = 0
LED2.off()

def map_value(x, in_min, in_max, out_min, out_max):
    return int((x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min)

def set_motor(left_speed, right_speed):
    # 左电机控制
    if left_speed >= 0:
        MOTOR_A1.duty(map_value(left_speed, 0, 100, 0, 1023))
        MOTOR_A2.duty(0)
    else:
        MOTOR_A1.duty(0)
        MOTOR_A2.duty(map_value(-left_speed, 0, 100, 0, 1023))
    
    # 右电机控制
    if right_speed >= 0:
        MOTOR_B1.duty(map_value(right_speed, 0, 100, 0, 1023))
        MOTOR_B2.duty(0)
    else:
        MOTOR_B1.duty(0)
        MOTOR_B2.duty(map_value(-right_speed, 0, 100, 0, 1023))
    
    print(f"电机设置: L={left_speed}%, R={right_speed}%")

def update_motor():
    global current_flag, pwm1, pwm2
    if current_flag == 1:       # 前进
        set_motor(pwm1, pwm2)
    elif current_flag == 2:     # 后退
        set_motor(-pwm1, -pwm2)
    elif current_flag == 3:     # 左转
        set_motor(0, pwm2)
    elif current_flag == 4:     # 右转
        set_motor(pwm1, 0)
    elif current_flag == 6:     # 前右转
        set_motor(pwm1, -pwm2)
    elif current_flag == 7:     # 前左转
        set_motor(-pwm1, pwm2)
    elif current_flag == 8:     # 后右转
        set_motor(pwm1, -pwm2)
    elif current_flag == 9:     # 后左转
        set_motor(-pwm1, pwm2)

def handle_command(cmd):
    global current_flag, pwm1, pwm2
    cmd = cmd.strip()
    print("接收到命令:", cmd)
    
    if cmd == 'F':
        current_flag = 1
        set_motor(pwm1, pwm2)
    elif cmd == 'B':
        current_flag = 2
        set_motor(-pwm1, -pwm2)
    elif cmd == 'L':
        current_flag = 3
        set_motor(0, pwm2)
    elif cmd == 'R':
        current_flag = 4
        set_motor(pwm1, 0)
    elif cmd == 'S':
        current_flag = 5
        set_motor(0, 0)
    elif cmd == 'FR':
        current_flag = 6
        set_motor(pwm1, -pwm2)
    elif cmd == 'FL':
        current_flag = 7
        set_motor(-pwm1, pwm2)
    elif cmd == 'BR':
        current_flag = 8
        set_motor(pwm1, -pwm2)
    elif cmd == 'BL':
        current_flag = 9
        set_motor(-pwm1, pwm2)
    elif cmd == 'BAG_OPEN':
        BAG.value(0)
    elif cmd == 'BAG_CLOSE':
        BAG.value(1)
    elif cmd == 'BRUSH_ON':
        BRUSH.value(0)
    elif cmd == 'BRUSH_OFF':
        BRUSH.value(1)
    elif cmd.startswith('P1:'):
        pwm1 = int(cmd[3:])
        update_motor()
    elif cmd.startswith('P2:'):
        pwm2 = int(cmd[3:])
        update_motor()

# 初始化外设
BRUSH.value(1)  # 默认关闭
BAG.value(1)    # 默认关闭

# 连接WiFi
wlan = network.WLAN(network.STA_IF)

def reset_wifi():
    global wlan
    try:
        wlan.disconnect()
        wlan.active(False)
        time.sleep(1)
        wlan.active(True)
        time.sleep(1)
        return True
    except Exception as e:
        print("重置WiFi失败:", e)
        return False

def connect_wifi():
    global wlan
    if not wlan.isconnected():
        print('正在连接WiFi中...')
        LED2.off()
        
        # 确保WiFi处于活动状态
        if not wlan.active():
            wlan.active(True)
            time.sleep(1)
        
        try:
            wlan.connect(SSID, PASSWORD)
            max_wait = 10
            while max_wait > 0:
                if wlan.isconnected():
                    break
                max_wait -= 1
                time.sleep(1)
            
            if not wlan.isconnected():
                print('WiFi连接失败，正在重置WiFi...')
                if reset_wifi():
                    time.sleep(2)
                    return False
                else:
                    print('WiFi重置失败，等待重试...')
                    time.sleep(5)
                    return False
            else:
                print('连接到WiFi:', wlan.ifconfig())
                LED2.on()
                return True
        except Exception as e:
            print('WiFi连接错误:', e)
            reset_wifi()
            time.sleep(2)
            return False
    return True

# 初始连接
if not connect_wifi():
    print("初始WiFi连接失败，请检查网络设置")
    time.sleep(5)

# WebSocket处理函数
def handle_websocket(websocket, addr):
    print(f"新的WebSocket连接来自: {addr}")
    try:
        while True:
            data = websocket.recv()
            if not data:
                break
            handle_command(data.decode())
            websocket.send("OK")
    except Exception as e:
        print(f"WebSocket错误: {e}")
    finally:
        websocket.close()
        print(f"WebSocket连接关闭: {addr}")

# 创建WebSocket服务器
server = websocket.WebSocketServer(WS_PORT)
print(f"WebSocket服务器启动在端口： {WS_PORT}")
LED2.on()

# 主循环
while True:
    try:
        # 接受新的连接
        websocket, addr = server.accept()
        # 在新线程中处理连接
        _thread.start_new_thread(handle_websocket, (websocket, addr))
    except Exception as e:
        print(f"服务器错误: {e}")
    
    # 维持WiFi连接
    if not wlan.isconnected():
        LED2.off()
        print("WiFi断开，正在重新连接...")
        if not connect_wifi():
            print("重连失败，等待5秒后重试...")
            time.sleep(5) 