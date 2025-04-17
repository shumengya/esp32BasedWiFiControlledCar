extends Node2D

var tcp = StreamPeerTCP.new()
var connected = false
var ip_address = "47.108.90.0"
var port = 8080
var connection_timeout = 2.0  # 秒
var connect_start_time = 0.0

# UI组件引用
@onready var ip_input = $IPInput
@onready var port_input = $PortInput
@onready var connect_button = $ConnectButton
@onready var status_label = $StatusLabel
@onready var pwm1_slider = $PWM1Slider
@onready var pwm2_slider = $PWM2Slider

# 电机参数
var pwm1 = 80
var pwm2 = 80

# 方向控制状态
var moving_forward = false
var moving_backward = false
var moving_left = false
var moving_right = false

func _ready():
	port_input.text = str(port)
	ip_input.text = ip_address
	pwm1_slider.value = pwm1
	pwm2_slider.value = pwm2
	
	connect_button.pressed.connect(_on_connect_button_pressed)
	pwm1_slider.value_changed.connect(_on_pwm1_changed)
	pwm2_slider.value_changed.connect(_on_pwm2_changed)

func _process(delta):
	_update_connection()
	_receive_data()

# 新增：统一连接状态管理
func _update_connection():
	var status = tcp.get_status()
	
	# 自动检测断开连接
	if connected && status != StreamPeerTCP.STATUS_CONNECTED:
		_handle_disconnect()
		return
	
	# 处理连接超时
	if status == StreamPeerTCP.STATUS_CONNECTING:
		if Time.get_ticks_msec() - connect_start_time > connection_timeout * 1000:
			_handle_connection_fail("连接超时")
	
	# 定期轮询更新状态
	tcp.poll()

# 新增：接收数据处理
func _receive_data():
	if connected:
		var available = tcp.get_available_bytes()
		if available > 0:
			# 读取所有可用字节
			var data = tcp.get_data(available)
			if data[0] == OK:
				var response = data[1].get_string_from_utf8()
				print("收到响应: ", response)
				# 这里可以添加协议解析逻辑

# 改进的连接方法
func connect_to_esp32():
	# 清理旧连接
	if tcp.get_status() != StreamPeerTCP.STATUS_NONE:
		tcp.disconnect_from_host()
		await get_tree().process_frame  # 等待一帧
	
	ip_address = ip_input.text
	port = int(port_input.text)
	
	print("尝试连接到: ", ip_address, ":", port)
	
	var error = tcp.connect_to_host(ip_address, port)
	if error != OK:
		_handle_connection_fail("连接失败: %d" % error)
		return
	
	connect_start_time = Time.get_ticks_msec()
	status_label.text = "连接中..."
	status_label.modulate = Color(1, 1, 0)

# 处理连接失败
func _handle_connection_fail(reason):
	print(reason)
	tcp.disconnect_from_host()
	connected = false
	status_label.text = reason
	status_label.modulate = Color(1, 0, 0)

# 处理断开连接
func _handle_disconnect():
	print("连接断开")
	connected = false
	tcp.disconnect_from_host()
	status_label.text = "已断开"
	status_label.modulate = Color(1, 0, 0)

# 改进的发送方法
func send_command(cmd):
	if tcp.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		_handle_disconnect()
		return
	
	# 添加换行符作为命令终止符
	var full_cmd = cmd + "\n"
	var error = tcp.put_data(full_cmd.to_utf8_buffer())
	
	if error != OK:
		print("发送失败: ", error)
		_handle_disconnect()
	else:
		print("已发送: ", cmd)

# 按钮回调
func _on_connect_button_pressed():
	if connected:
		tcp.disconnect_from_host()
		connect_button.text = "连接"
	else:
		connect_to_esp32()
		connect_button.text = "断开"



func _on_pwm1_changed(value):
	pwm1 = int(value)
	send_command("P1:" + str(pwm1))

func _on_pwm2_changed(value):
	pwm2 = int(value)
	send_command("P2:" + str(pwm2))

# 新的按钮控制方案 - 按下按钮时调用
func _on_forward_button_down():
	moving_forward = true
	moving_backward = false  # 防止同时按下相反方向
	send_command("F")
	print("开始前进")

func _on_forward_button_up():
	moving_forward = false
	if not (moving_backward or moving_left or moving_right):
		send_command("S")  # 如果没有其他方向按下，则停止
		print("停止前进")

func _on_back_button_down():
	moving_backward = true
	moving_forward = false  # 防止同时按下相反方向
	send_command("B")
	print("开始后退")

func _on_back_button_up():
	moving_backward = false
	if not (moving_forward or moving_left or moving_right):
		send_command("S")  # 如果没有其他方向按下，则停止
		print("停止后退")

func _on_left_button_down():
	moving_left = true
	moving_right = false  # 防止同时按下相反方向
	send_command("L")
	print("开始左转")

func _on_left_button_up():
	moving_left = false
	if not (moving_forward or moving_backward or moving_right):
		send_command("S")  # 如果没有其他方向按下，则停止
		print("停止左转")

func _on_right_button_down():
	moving_right = true
	moving_left = false  # 防止同时按下相反方向
	send_command("R")
	print("开始右转")

func _on_right_button_up():
	moving_right = false
	if not (moving_forward or moving_backward or moving_left):
		send_command("S")  # 如果没有其他方向按下，则停止
		print("停止右转")

# 保留原有的单击按钮功能，但重命名以避免冲突
func _on_forward_pressed():
	send_command("F")

func _on_back_pressed():
	send_command("B")

func _on_stop_pressed():
	# 停止所有移动
	moving_forward = false
	moving_backward = false
	moving_left = false
	moving_right = false
	send_command("S")

func _on_left_pressed():
	send_command("L")

func _on_right_pressed():
	send_command("R")

func _on_brush_on_pressed():
	send_command("BRUSH_ON")

func _on_brush_off_pressed():
	send_command("BRUSH_OFF")

func _on_bag_open_pressed():
	send_command("BAG_OPEN")

func _on_bag_close_pressed():
	send_command("BAG_CLOSE")

func _on_connection_success():
	connected = true
	status_label.text = "已连接"
	status_label.modulate = Color(0, 1, 0)
	print("连接成功")
	test_motors()

# 修改后的测试方法
func test_motors():
	if !connected: return
	
	var test_sequence = [
		{"cmd": "P1:100", "delay": 0.3},
		{"cmd": "P2:100", "delay": 0.3},
		{"cmd": "F", "delay": 0.5},
		{"cmd": "S", "delay": 0.5},
		{"cmd": "B", "delay": 0.5},
		{"cmd": "S", "delay": 0.5}
	]
	
	for step in test_sequence:
		send_command(step.cmd)
		await get_tree().create_timer(step.delay).timeout
  
func _input(event):
	if not connected:
		return
		
	if event is InputEventKey:
		# 按下按键
		if event.pressed and not event.echo:
			match event.keycode:
				KEY_W, KEY_UP:
					_on_forward_button_down()
				KEY_S, KEY_DOWN:
					_on_back_button_down()
				KEY_A, KEY_LEFT:
					_on_left_button_down()
				KEY_D, KEY_RIGHT:
					_on_right_button_down()
				KEY_SPACE:
					_on_stop_pressed()
		
		# 释放按键
		elif not event.pressed:
			match event.keycode:
				KEY_W, KEY_UP:
					_on_forward_button_up()
				KEY_S, KEY_DOWN:
					_on_back_button_up()
				KEY_A, KEY_LEFT:
					_on_left_button_up()
				KEY_D, KEY_RIGHT:
					_on_right_button_up()

func _on_right_2_button_down():
	_on_right_button_down()
func _on_right_2_button_up():
	_on_right_button_up()

func _on_left_2_button_down():
	_on_left_button_down()
func _on_left_2_button_up():
	_on_left_button_up()

func _on_back_2_button_down():
	_on_back_button_down()
func _on_back_2_button_up():
	_on_back_button_up()

func _on_foward_2_button_down():
	_on_forward_button_down()
func _on_foward_2_button_up():
	_on_forward_button_up()

#在这里修改 - 前右转弯（左轮前进，右轮后退）
func _on_foward_right_button_down():
	# 设置移动状态
	moving_forward = true
	moving_right = true
	moving_backward = false
	moving_left = false
	
	# 发送自定义命令 - 左轮前进，右轮后退
	send_command("P1:100")  # 设置左轮PWM为最大
	send_command("P2:100")  # 设置右轮PWM为最大
	
	# 左轮前进，右轮后退
	send_command("FR")  # 如果ESP32支持这个命令
	# 如果ESP32不支持FR命令，可以使用以下替代方案
	# 直接控制电机
	# 这需要在ESP32上添加相应的命令处理
	print("开始前右转")

func _on_foward_right_button_up():
	moving_forward = false
	moving_right = false
	if not (moving_backward or moving_left):
		send_command("S")  # 停止
	print("停止前右转")

# 后左转弯（左轮后退，右轮前进）
func _on_back_left_button_down():
	# 设置移动状态
	moving_backward = true
	moving_left = true
	moving_forward = false
	moving_right = false
	
	# 发送自定义命令 - 左轮后退，右轮前进
	send_command("P1:100")  # 设置左轮PWM为最大
	send_command("P2:100")  # 设置右轮PWM为最大
	
	# 左轮后退，右轮前进
	send_command("BL")  # 如果ESP32支持这个命令
	# 如果ESP32不支持BL命令，可以使用以下替代方案
	# 直接控制电机
	# 这需要在ESP32上添加相应的命令处理
	print("开始后左转")

func _on_back_left_button_up():
	moving_backward = false
	moving_left = false
	if not (moving_forward or moving_right):
		send_command("S")  # 停止
	print("停止后左转")

# 前左转弯（左轮后退，右轮前进）
func _on_foward_left_button_down():
	# 设置移动状态
	moving_forward = true
	moving_left = true
	moving_backward = false
	moving_right = false
	
	# 发送自定义命令 - 左轮后退，右轮前进
	send_command("P1:100")  # 设置左轮PWM为最大
	send_command("P2:100")  # 设置右轮PWM为最大
	
	# 左轮后退，右轮前进
	send_command("FL")  # 如果ESP32支持这个命令
	# 如果ESP32不支持FL命令，可以使用以下替代方案
	# 直接控制电机
	# 这需要在ESP32上添加相应的命令处理
	print("开始前左转")

func _on_foward_left_button_up():
	moving_forward = false
	moving_left = false
	if not (moving_backward or moving_right):
		send_command("S")  # 停止
	print("停止前左转")

# 后右转弯（左轮前进，右轮后退）
func _on_back_right_button_down():
	# 设置移动状态
	moving_backward = true
	moving_right = true
	moving_forward = false
	moving_left = false
	
	# 发送自定义命令 - 左轮前进，右轮后退
	send_command("P1:100")  # 设置左轮PWM为最大
	send_command("P2:100")  # 设置右轮PWM为最大
	
	# 左轮前进，右轮后退
	send_command("BR")  # 如果ESP32支持这个命令
	# 如果ESP32不支持BR命令，可以使用以下替代方案
	# 直接控制电机
	# 这需要在ESP32上添加相应的命令处理
	print("开始后右转")

func _on_back_right_button_up():
	moving_backward = false
	moving_right = false
	if not (moving_forward or moving_left):
		send_command("S")  # 停止
	print("停止后右转")
#StreamPeerTCP
