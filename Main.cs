using Godot;
using System;
using System.Collections.Generic;

public partial class Main : Node2D
{
	private StreamPeerTcp tcp = new StreamPeerTcp();
	private bool connected = false;
	private string ipAddress = "192.168.243.49";
	private int port = 8080;
	private float connectionTimeout = 2.0f;  // 秒
	private double connectStartTime = 0.0;
	private bool isProcessingCommand = false; // 用于非按压式控制
	private ulong lastStopTime = 0; // 新增：上次停止命令的时间
	private bool isStopPending = false; // 新增：是否有待执行的停止命令
	private string currentCommand = ""; // 当前正在发送的命令
	private ulong lastCommandTime = 0; // 上次发送命令的时间
	private const ulong COMMAND_INTERVAL = 500; // 命令发送间隔（毫秒）

	// UI组件引用
	private LineEdit ipInput;
	private LineEdit portInput;
	private LineEdit delayedInput;
	private Button connectButton;
	private Button OKBtn;
	private Label statusLabel;
	private HSlider pwm1Slider;
	private HSlider pwm2Slider;

	//点击式控制
	private Button stopButton;
	private Button forwardButton;
	private Button backwardButton;

	private Button left45Btn;
	private Button left90Btn;
	private Button left180Btn;
	private Button right45Btn;
	private Button right90Btn;
	private Button right180Btn;

	//按压式控制
	private Button stopBtn;
	private Button forwardBtn;
	private Button backwardBtn;
	private Button leftBtn;
	private Button rightBtn;
	

	// 电机参数
	private int pwm1 = 60;
	private int pwm2 = 60;
	private int delayedTime = 260;

	// 方向控制状态
	private bool movingForward = false;
	private bool movingBackward = false;
	private bool movingLeft = false;
	private bool movingRight = false;

	public override void _Ready()
	{
		// 获取节点引用
		ipInput = GetNode<LineEdit>("IPInput");
		portInput = GetNode<LineEdit>("PortInput");
		delayedInput = GetNode<LineEdit>("DelayedInput");
		connectButton = GetNode<Button>("ConnectButton");
		OKBtn = GetNode<Button>("OKBtn");
		statusLabel = GetNode<Label>("StatusLabel");
		pwm1Slider = GetNode<HSlider>("PWM1Slider");
		pwm2Slider = GetNode<HSlider>("PWM2Slider");

		// 初始化UI
		portInput.Text = port.ToString();
		ipInput.Text = ipAddress;
		pwm1Slider.Value = pwm1;
		pwm2Slider.Value = pwm2;

		// 连接信号
		connectButton.Pressed += OnConnectButtonPressed;
		OKBtn.Pressed += OnOKBtnPressed;
		pwm1Slider.ValueChanged += OnPwm1Changed;
		pwm2Slider.ValueChanged += OnPwm2Changed;

		stopButton = GetNode<Button>("clickBasedControl/StopButton");
		forwardButton = GetNode<Button>("clickBasedControl/ForwardButton");
		backwardButton = GetNode<Button>("clickBasedControl/BackwardButton");

		forwardBtn = GetNode<Button>("pressBasedControl/ForwardBtn");
		backwardBtn = GetNode<Button>("pressBasedControl/BackwardBtn");
		leftBtn = GetNode<Button>("pressBasedControl/LeftBtn");
		rightBtn = GetNode<Button>("pressBasedControl/RightBtn");

		stopButton.Pressed += OnStopPressed;
		forwardButton.Pressed += OnForwardPressed;
		backwardButton.Pressed += OnBackPressed;

        left45Btn = GetNode<Button>("clickBasedControl/Left45Btn");
        left90Btn = GetNode<Button>("clickBasedControl/Left90Btn");
        left180Btn = GetNode<Button>("clickBasedControl/Left180Btn");
        right45Btn = GetNode<Button>("clickBasedControl/Right45Btn");
        right90Btn = GetNode<Button>("clickBasedControl/Right90Btn");
        right180Btn = GetNode<Button>("clickBasedControl/Right180Btn");
        

		forwardBtn.ButtonDown += OnForwardButtonDown;
		forwardBtn.ButtonUp += OnForwardButtonUp;
		backwardBtn.ButtonDown += OnBackButtonDown;
		backwardBtn.ButtonUp += OnBackButtonUp;
		leftBtn.ButtonDown += OnLeftButtonDown;
		leftBtn.ButtonUp += OnLeftButtonUp;
		rightBtn.ButtonDown += OnRightButtonDown;
		rightBtn.ButtonUp += OnRightButtonUp;
		
		// 连接新的旋转按钮事件
		left45Btn.Pressed += OnLeft45Pressed;
		left90Btn.Pressed += OnLeft90Pressed;
		left180Btn.Pressed += OnLeft180Pressed;
		right45Btn.Pressed += OnRight45Pressed;
		right90Btn.Pressed += OnRight90Pressed;
		right180Btn.Pressed += OnRight180Pressed;

	}

	public override void _Process(double delta)
	{
		UpdateConnection();
		ReceiveData();
		CheckStopCommand();
		SendContinuousCommand();
	}

	private void UpdateConnection()
	{
		var status = tcp.GetStatus();

		// 自动检测断开连接
		if (connected && status != StreamPeerTcp.Status.Connected)
		{
			HandleDisconnect();
			return;
		}

		// 处理连接成功
		if (!connected && status == StreamPeerTcp.Status.Connected)
		{
			OnConnectionSuccess();
			return;
		}

		// 处理连接超时
		if (status == StreamPeerTcp.Status.Connecting)
		{
			if (Time.GetTicksMsec() - connectStartTime > connectionTimeout * 1000)
			{
				HandleConnectionFail("连接超时");
			}
		}

		// 定期轮询更新状态
		tcp.Poll();
	}

	private void ReceiveData()
	{
		if (connected)
		{
			var available = tcp.GetAvailableBytes();
			if (available > 0)
			{
				// 读取所有可用字节
				var data = tcp.GetData(available);
				if (data[0].AsInt32() == 0)
				{
					var response = ((byte[])data[1]).GetStringFromUtf8();
					GD.Print("收到响应: ", response);
					isProcessingCommand = false; // 命令处理完成
				}
			}
		}
	}

	private async void ConnectToEsp32()
	{
		// 清理旧连接
		if (tcp.GetStatus() != StreamPeerTcp.Status.None)
		{
			tcp.DisconnectFromHost();
			await ToSignal(GetTree(), "process_frame");
		}

		ipAddress = ipInput.Text;
		port = int.Parse(portInput.Text);

		GD.Print("尝试连接到: ", ipAddress, ":", port);

		var error = tcp.ConnectToHost(ipAddress, port);
		if (error != Error.Ok)
		{
			HandleConnectionFail($"连接失败: {error}");
			return;
		}

		connectStartTime = Time.GetTicksMsec();
		statusLabel.Text = "连接中...";
		statusLabel.Modulate = new Color(1, 1, 0);
		
		// 等待连接完成
		ulong timeout = Time.GetTicksMsec() + (ulong)((long)(connectionTimeout * 1000));
		while (tcp.GetStatus() == StreamPeerTcp.Status.Connecting)
		{
			if (Time.GetTicksMsec() > timeout)
			{
				HandleConnectionFail("连接超时");
				return;
			}
			await ToSignal(GetTree(), "process_frame");
		}

		// 连接成功后等待一小段时间确保连接稳定
		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
	}

	private void HandleConnectionFail(string reason)
	{
		GD.Print(reason);
		tcp.DisconnectFromHost();
		connected = false;
		statusLabel.Text = reason;
		statusLabel.Modulate = new Color(1, 0, 0);
	}

	private void HandleDisconnect()
	{
		GD.Print("连接断开");
		connected = false;
		isProcessingCommand = false; // 重置命令处理标志
		tcp.DisconnectFromHost();
		statusLabel.Text = "已断开";
		statusLabel.Modulate = new Color(1, 0, 0);
	}

	private void SendContinuousCommand()
	{
		if (!connected || string.IsNullOrEmpty(currentCommand)) return;

		ulong currentTime = Time.GetTicksMsec();
		if (currentTime - lastCommandTime >= COMMAND_INTERVAL)
		{
			SendCommand(currentCommand, false);
			lastCommandTime = currentTime;
		}
	}

	private void SendCommand(string cmd, bool waitForResponse = true)
	{
		if (tcp.GetStatus() != StreamPeerTcp.Status.Connected)
		{
			HandleDisconnect();
			return;
		}

		if (waitForResponse && isProcessingCommand)
		{
			GD.Print("上一个命令正在处理中，跳过命令: ", cmd);
			return;
		}

		if (waitForResponse)
		{
			isProcessingCommand = true;
		}

		// 添加换行符作为命令终止符
		var fullCmd = cmd + "\n";
		var error = tcp.PutData(fullCmd.ToUtf8Buffer());

		if (error != Error.Ok)
		{
			GD.Print("发送失败: ", error);
			HandleDisconnect();
		}
		else
		{
			GD.Print("已发送: ", cmd);
		}
	}

	private void OnConnectButtonPressed()
	{
		if (connected)
		{
			tcp.DisconnectFromHost();
			connectButton.Text = "连接";
		}
		else
		{
			ConnectToEsp32();
			connectButton.Text = "断开";
		}
	}

	private void OnPwm1Changed(double value)
	{
		pwm1 = (int)value;
		SendCommand("P1:" + pwm1, true);
	}

	private void OnPwm2Changed(double value)
	{
		pwm2 = (int)value;
		SendCommand("P2:" + pwm2, true);
	}



	// 方向控制方法
	private void OnForwardButtonDown()
	{
		if (!connected) return;
		movingForward = true;
		movingBackward = false;
		currentCommand = "F";
		SendCommand(currentCommand, false);
		GD.Print("开始前进");
	}

	private void OnForwardButtonUp()
	{
		if (!connected) return;
		movingForward = false;
		currentCommand = "S";
		SendCommand(currentCommand, false);
		GD.Print("停止前进");
	}

	private void OnBackButtonDown()
	{
		if (!connected) return;
		movingBackward = true;
		movingForward = false;
		currentCommand = "B";
		SendCommand(currentCommand, false);
		GD.Print("开始后退");
	}

	private void OnBackButtonUp()
	{
		if (!connected) return;
		movingBackward = false;
		currentCommand = "S";
		SendCommand(currentCommand, false);
		GD.Print("停止后退");
	}

	private void OnLeftButtonDown()
	{
		if (!connected) return;
		movingLeft = true;
		movingRight = false;
		currentCommand = "L";
		SendCommand(currentCommand, false);
		GD.Print("开始左转");
	}

	private void OnLeftButtonUp()
	{
		if (!connected) return;
		movingLeft = false;
		currentCommand = "S";
		SendCommand(currentCommand, false);
		GD.Print("停止左转");
	}
 
	private void OnRightButtonDown()
	{
		if (!connected) return;
		movingRight = true;
		movingLeft = false;
		currentCommand = "R";
		SendCommand(currentCommand, false);
		GD.Print("开始右转");
	}

	private void OnRightButtonUp()
	{
		if (!connected) return;
		movingRight = false;
		currentCommand = "S";
		SendCommand(currentCommand, false);
		GD.Print("停止右转");
	}

	
	// 其他控制方法
	private void OnForwardPressed()
	{
		SendCommand("F");
	}

	private void OnBackPressed()
	{
		SendCommand("B");
	}

	private void OnStopPressed()
	{
		if (!connected) return;
		movingForward = false;
		movingBackward = false;
		movingLeft = false;
		movingRight = false;
		isProcessingCommand = false; // 重置命令处理标志
		SendCommand("S");
		GD.Print("强制停止所有移动");
	}

	private void OnLeftPressed()
	{
		SendCommand("L");
	}

	private void OnRightPressed()
	{
		SendCommand("R");
	}

	private void OnBrushOnPressed()
	{
		SendCommand("BRUSH_ON", true);
	}

	private void OnBrushOffPressed()
	{
		SendCommand("BRUSH_OFF", true);
	}

	private void OnBagOpenPressed()
	{
		SendCommand("BAG_OPEN", true);
	}

	private void OnBagClosePressed()
	{
		SendCommand("BAG_CLOSE", true);
	}

	private void OnConnectionSuccess()
	{
		connected = true;
		statusLabel.Text = "已连接";
		statusLabel.Modulate = new Color(0, 1, 0);
		GD.Print("连接成功");
	}

	private void OnStopDelayTimeout()
	{
		if (!connected) return;
		SendCommand("S", false);
		GD.Print("延迟停止");
	}

	private void CheckStopCommand()
	{
		if (isStopPending && Time.GetTicksMsec() - lastStopTime >= (ulong)delayedTime) // 将 delayedTime 转换为 ulong
		{
			if (connected)
			{
				SendCommand("S", false);
				GD.Print("延迟停止");
			}
			isStopPending = false;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!connected) return;

		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.Pressed && !keyEvent.Echo)
			{
				switch (keyEvent.Keycode)
				{
					case Key.W:
					case Key.Up:
						OnForwardButtonDown();
						break;
					case Key.S:
					case Key.Down:
						OnBackButtonDown();
						break;
					case Key.A:
					case Key.Left:
						OnLeftButtonDown();
						break;
					case Key.D:
					case Key.Right:
						OnRightButtonDown();
						break;
					case Key.Space:
						OnStopPressed();
						break;
				}
			}
			else if (!keyEvent.Pressed)
			{
				switch (keyEvent.Keycode)
				{
					case Key.W:
					case Key.Up:
						OnForwardButtonUp();
						break;
					case Key.S:
					case Key.Down:
						OnBackButtonUp();
						break;
					case Key.A:
					case Key.Left:
						OnLeftButtonUp();
						break;
					case Key.D:
					case Key.Right:
						OnRightButtonUp();
						break;
				}
			}
		}
	}




	private void OnOKBtnPressed()
	{
		delayedTime = int.Parse(delayedInput.Text);
	}

	private void OnLeft45Pressed()
	{
		if (!connected) return;
		SendCommand("L45", true);
		GD.Print("左转45度");
	}

	private void OnLeft90Pressed()
	{
		if (!connected) return;
		SendCommand("L90", true);
		GD.Print("左转90度");
	}

	private void OnLeft180Pressed()
	{
		if (!connected) return;
		SendCommand("L180", true);
		GD.Print("左转180度");
	}

	private void OnRight45Pressed()
	{
		if (!connected) return;
		SendCommand("R45", true);
		GD.Print("右转45度");
	}

	private void OnRight90Pressed()
	{
		if (!connected) return;
		SendCommand("R90", true);
		GD.Print("右转90度");
	}

	private void OnRight180Pressed()
	{
		if (!connected) return;
		SendCommand("R180", true);
		GD.Print("右转180度");
	}

}
