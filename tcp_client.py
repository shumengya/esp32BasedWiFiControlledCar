import socket
import time

# ESP32的IP地址和端口
ESP32_IP = "192.168.243.49"
ESP32_PORT = 8080

def connect_to_esp32():
    # 创建TCP socket
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    
    try:
        print(f"尝试连接到 {ESP32_IP}:{ESP32_PORT}")
        # 设置连接超时
        client_socket.settimeout(5)
        # 连接到ESP32
        client_socket.connect((ESP32_IP, ESP32_PORT))
        print("连接成功！")
        
        # 测试发送命令
        test_commands = [
            "F",  # 前进
            "S",  # 停止
            "B",  # 后退
            "S",  # 停止
            "L",  # 左转
            "S",  # 停止
            "R",  # 右转
            "S"   # 停止
        ]
        
        for cmd in test_commands:
            print(f"发送命令: {cmd}")
            client_socket.send((cmd + "\n").encode())
            
            # 等待响应
            try:
                response = client_socket.recv(1024)
                print(f"收到响应: {response.decode().strip()}")
            except socket.timeout:
                print("等待响应超时")
            
            time.sleep(1)  # 等待1秒
        
    except socket.timeout:
        print("连接超时")
    except ConnectionRefusedError:
        print("连接被拒绝，请检查ESP32是否正在运行")
    except Exception as e:
        print(f"发生错误: {e}")
    finally:
        # 关闭连接
        client_socket.close()
        print("连接已关闭")

if __name__ == "__main__":
    connect_to_esp32() 