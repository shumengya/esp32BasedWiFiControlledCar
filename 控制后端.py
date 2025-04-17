import network
import socket
from machine import Pin, PWM
import time

# WiFi配置
SSID = 'shumengya'
PASSWORD = '0123456789'
TCP_PORT = 8080

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
isAutoForward = True


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
    global current_flag, pwm1, pwm2, isAutoForward
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

    elif cmd == 'L10':
        leftTurnTypeControl(0.1)
    elif cmd == 'L15':
        leftTurnTypeControl(0.2)
    elif cmd == 'L30':
        leftTurnTypeControl(0.3)
    elif cmd == 'L45':
        leftTurnTypeControl(0.5)
    elif cmd == 'L90':
        leftTurnTypeControl(1.0)
    elif cmd == 'L180':
        leftTurnTypeControl(2.0)

    elif cmd == 'R10':
        rightTurnTypeControl(0.1)
    elif cmd == 'R15':
        rightTurnTypeControl(0.2)
    elif cmd == 'R30':
        rightTurnTypeControl(0.3)
    elif cmd == 'R45':
        rightTurnTypeControl(0.5)
    elif cmd == 'R90':
        rightTurnTypeControl(1.0)
    elif cmd == 'R180':
        rightTurnTypeControl(2.0)

    elif cmd == 'BAG_OPEN':
        BAG.value(0)
    elif cmd == 'BAG_CLOSE':
        BAG.value(1)
    elif cmd == 'BRUSH_ON':
        BRUSH.value(0)
    elif cmd == 'BRUSH_OFF':
        BRUSH.value(1)
    elif cmd.startswith('P:'):
        try:
            # 解析格式为 "P:60,60" 的命令
            values = cmd[2:].split(',')
            if len(values) == 2:
                pwm1 = int(values[0])
                pwm2 = int(values[1])
                # 确保值在0-100范围内
                pwm1 = max(0, min(100, pwm1))
                pwm2 = max(0, min(100, pwm2))
                update_motor()
                print(f"设置电机速度: P1={pwm1}%, P2={pwm2}%")
        except Exception as e:
            print("PWM设置错误:", e)
    elif cmd == 'AF':
        isAutoForward = True
        print("自动前进已开启")
    elif cmd == 'AF_OFF':
        isAutoForward = False
        print("自动前进已关闭")
    else:
        print("未知命令:", cmd)

# 初始化外设
BRUSH.value(1)  # 默认关闭
BAG.value(1)    # 默认关闭
wlan = network.WLAN(network.STA_IF)

# 连接WiFi
def handleAutoForward(isAutoForward):
    global current_flag, pwm1, pwm2
    if isAutoForward:
        current_flag = 1
        set_motor(pwm1, pwm2)

def leftTurnTypeControl(delayTime):
    global current_flag, pwm2
    current_flag = 3
    set_motor(0, pwm2)
    time.sleep(delayTime)  
    set_motor(0, 0)
    if isAutoForward:
        handleAutoForward(isAutoForward)

def rightTurnTypeControl(delayTime):
    global current_flag, pwm1
    current_flag = 4
    set_motor(pwm1, 0)
    time.sleep(delayTime)  
    set_motor(0, 0)
    if isAutoForward:
        handleAutoForward(isAutoForward)

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

# 创建TCP Socket
try:
    # 尝试关闭可能存在的旧连接
    try:
        old_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        old_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        old_sock.close()
    except:
        pass

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)  # 添加这行来允许端口重用
    sock.bind(('0.0.0.0', TCP_PORT))
    sock.listen(1)
    print(f"TCP服务器启动在端口： {TCP_PORT}")
    LED2.on()
except Exception as e:
    print("TCP服务器启动失败:", e)
    time.sleep(5)
    machine.reset()  # 如果无法启动服务器，重启设备

# 主循环
while True:
    try:
        # 接受新的连接
        conn, addr = sock.accept()
        print(f"新的连接来自: {addr}")
        LED2.on()  # TCP连接后LED2常亮
        
        while True:
            try:
                data = conn.recv(256)
                if not data:
                    break
                    
                cmd = data.decode().strip()
                handle_command(cmd)
                conn.send(b'OK\n')
            except Exception as e:
                print("处理命令时出错:", e)
                break
                
        conn.close()
        print("连接已关闭")
        LED2.off()  # 连接关闭后LED2熄灭
        
    except Exception as e:
        print("服务器错误:", e)
        try:
            sock.close()  # 确保关闭socket
        except:
            pass
        time.sleep(1)  # 等待一秒后重试
        continue  # 继续主循环



