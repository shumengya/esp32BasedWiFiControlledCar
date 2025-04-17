using Godot;
using System;
using System.Collections.Generic;

public partial class Main : Node2D
{
	private StreamPeerTcp tcp = new StreamPeerTcp();
	private bool connected = false;
	private string ipAddress = "47.108.90.0";
	private int port = 9090;
	private float connectionTimeout = 2.0f;  // 秒
	private double connectStartTime = 0.0;
	private bool isProcessingCommand = false; // 用于非按压式控制
	private bool isAutoForwardAfterRotation = true;
	private string currentCommand = ""; // 当前正在发送的命令
	private ulong lastCommandTime = 0; // 上次发送命令的时间
	private const ulong COMMAND_INTERVAL = 500; // 命令发送间隔（毫秒）
	private const ulong STOP_INTERVAL = 300; // 停止命令最小间隔（毫秒）
	private ulong lastStopTime = 0; // 上次发送停止命令的时间

	// UI组件引用
	private LineEdit ipInput;
	private LineEdit portInput;
	private Button connectButton;
	private Button OKBtn;
	private Label statusLabel;
	private LineEdit pwm1Speed;
	private LineEdit pwm2Speed;
	private CheckButton toggleAutoForward;
	private Button rotationSpeedModifyBtn;

//-------------点击式控制-------------
	private Button stopButton;
	private Button forwardButton;
	private Button backwardButton;

	private Button left10Btn;
	private Button left15Btn;
	private Button left30Btn;
	private Button left45Btn;
	private Button left90Btn;
	private Button left180Btn;

	private Button right10Btn;
	private Button right15Btn;
	private Button right30Btn;
	private Button right45Btn;
	private Button right90Btn;
	private Button right180Btn;


//-------------按压式控制-------------
	private Button stopBtn;
	private Button forwardBtn;
	private Button backwardBtn;
	private Button leftBtn;
	private Button rightBtn;
	

	// 电机参数
	private int pwm1 = 60;
	private int pwm2 = 60;

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
		connectButton = GetNode<Button>("ConnectButton");
		statusLabel = GetNode<Label>("StatusLabel");
		pwm1Speed = GetNode<LineEdit>("PWM1Speed");
		pwm2Speed = GetNode<LineEdit>("PWM2Speed");
		toggleAutoForward = GetNode<CheckButton>("autoForwardAfterRotationBtn");
		rotationSpeedModifyBtn = GetNode<Button>("rotationSpeedModifyBtn");

		// 初始化UI
		portInput.Text = port.ToString();
		ipInput.Text = ipAddress;
		pwm1Speed.Text = pwm1.ToString();
		pwm2Speed.Text = pwm2.ToString();

		pwm1Speed.TextChanged += OnPwm1SpeedChanged;
		pwm2Speed.TextChanged += OnPwm2SpeedChanged;
		connectButton.Pressed += OnConnectButtonPressed;
		toggleAutoForward.Toggled += OnAutoForwardToggle;
		rotationSpeedModifyBtn.Pressed += OnRotationSpeedModifyPressed;
		//按压式控制
		forwardBtn = GetNode<Button>("pressBasedControl/ForwardBtn");
		backwardBtn = GetNode<Button>("pressBasedControl/BackwardBtn");
		leftBtn = GetNode<Button>("pressBasedControl/LeftBtn");
		rightBtn = GetNode<Button>("pressBasedControl/RightBtn");


		forwardBtn.ButtonDown += OnForwardButtonDown;
		forwardBtn.ButtonUp += OnForwardButtonUp;
		backwardBtn.ButtonDown += OnBackButtonDown;
		backwardBtn.ButtonUp += OnBackButtonUp;
		leftBtn.ButtonDown += OnLeftButtonDown;
		leftBtn.ButtonUp += OnLeftButtonUp;
		rightBtn.ButtonDown += OnRightButtonDown;
		rightBtn.ButtonUp += OnRightButtonUp;

		//点击式控制
		stopButton = GetNode<Button>("clickBasedControl/StopBtn");
		forwardButton = GetNode<Button>("clickBasedControl/ForwardBtn");
		backwardButton = GetNode<Button>("clickBasedControl/BackwardBtn");

		stopButton.Pressed += OnStopPressed;
		forwardButton.Pressed += OnForwardPressed;
		backwardButton.Pressed += OnBackPressed;

		left10Btn = GetNode<Button>("clickBasedControl/Left10Btn");
		left15Btn = GetNode<Button>("clickBasedControl/Left15Btn");
		left30Btn = GetNode<Button>("clickBasedControl/Left30Btn");
        left45Btn = GetNode<Button>("clickBasedControl/Left45Btn");
        left90Btn = GetNode<Button>("clickBasedControl/Left90Btn");
        left180Btn = GetNode<Button>("clickBasedControl/Left180Btn");

		right10Btn = GetNode<Button>("clickBasedControl/Right10Btn");
		right15Btn = GetNode<Button>("clickBasedControl/Right15Btn");
		right30Btn = GetNode<Button>("clickBasedControl/Right30Btn");		
        right45Btn = GetNode<Button>("clickBasedControl/Right45Btn");
        right90Btn = GetNode<Button>("clickBasedControl/Right90Btn");
        right180Btn = GetNode<Button>("clickBasedControl/Right180Btn");
        
		//连接点击式旋转按钮事件
		left10Btn.Pressed += () => leftTurnTypeControl("10");
		left15Btn.Pressed += () => leftTurnTypeControl("15");
		left30Btn.Pressed += () => leftTurnTypeControl("30");		
		left45Btn.Pressed += () => leftTurnTypeControl("45");
		left90Btn.Pressed += () => leftTurnTypeControl("90");
		left180Btn.Pressed += () => leftTurnTypeControl("180");
 
		right10Btn.Pressed += () => rightTurnTypeControl("10");
		right15Btn.Pressed += () => rightTurnTypeControl("15");
		right30Btn.Pressed += () => rightTurnTypeControl("30");
		right45Btn.Pressed += () => rightTurnTypeControl("45");
		right90Btn.Pressed += () => rightTurnTypeControl("90");
		right180Btn.Pressed += () => rightTurnTypeControl("180");

	}

    private void OnPwm1SpeedChanged(string newText)
    {
        if(newText.IsValidInt()){
            pwm1 = int.Parse(newText);
            if(pwm1 > 100){
                pwm1 = 100;
                pwm1Speed.Text = "100";
            }
            if(pwm2 > 100){
                pwm2 = 100;
                pwm2Speed.Text = "100";
            }
            if(pwm1 < 0){
                pwm1 = 0;
                pwm1Speed.Text = "0";
            }
            if(pwm2 < 0){
                pwm2 = 0;
                pwm2Speed.Text = "0";
            }
        }else{
            pwm1Speed.Text = "0";
            pwm2Speed.Text = "0";
        }
    }

    private void OnPwm2SpeedChanged(string newText)
    {
        if(newText.IsValidInt()){
            pwm2 = int.Parse(newText);
            if(pwm1 > 100){
                pwm1 = 100;
                pwm1Speed.Text = "100";
            }
            if(pwm2 > 100){
                pwm2 = 100;
                pwm2Speed.Text = "100";
            }
            if(pwm1 < 0){
                pwm1 = 0;
                pwm1Speed.Text = "0";
            }
            if(pwm2 < 0){
                pwm2 = 0;
                pwm2Speed.Text = "0";
            }
        }else{
            pwm1Speed.Text = "0";
            pwm2Speed.Text = "0";
        }
    }

    //电机速度控制方法

    private void OnRotationSpeedModifyPressed()
    {
        if (!connected) return;
        SendCommand($"P:{pwm1},{pwm2}", true);
    }


    private void OnAutoForwardToggle(bool toggledOn)
    {
		if (!connected) return;
        if (toggledOn == true){
			SendCommand("AF",true);
		}else{
			SendCommand("AF_OFF",true);
		}
    }

    public override void _Process(double delta)
	{
		UpdateConnection();
		ReceiveData();
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






	//按压式控制函数
	private void OnForwardButtonDown()
	{
		if (!connected) return;
		SendCommand("F", false);
		GD.Print("前进");
	}

	private void OnForwardButtonUp()
	{
		if (!connected) return;
		SendStopCommand();
	}

	private void OnBackButtonDown()
	{
		if (!connected) return;
		SendCommand("B", false);
		GD.Print("后退");
	}

	private void OnBackButtonUp()
	{
		if (!connected) return;
		SendStopCommand();
	}

	private void OnLeftButtonDown()
	{
		if (!connected) return;
		SendCommand("L", false);
		GD.Print("左转");
	}

	private void OnLeftButtonUp()
	{
		if (!connected) return;
		SendStopCommand();
	}
 
	private void OnRightButtonDown()
	{
		if (!connected) return;
		SendCommand("R", false);
		GD.Print("右转");
	}

	private void OnRightButtonUp()
	{
		if (!connected) return;
		SendStopCommand();
	}

	private void SendStopCommand()
	{
		ulong currentTime = Time.GetTicksMsec();
		if (currentTime - lastStopTime >= STOP_INTERVAL)
		{
			SendCommand("S", false);
			lastStopTime = currentTime;
			GD.Print("停止");
		}
	}

	
	//点击式控制函数
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


	private void leftTurnTypeControl(String command){
		if (!connected) return;
		SendCommand("L"+command,true);
		GD.Print("左转"+command+"度");
	}
	private void rightTurnTypeControl(String command){
		if (!connected) return;
		SendCommand("R"+command,true);
		GD.Print("右转"+command+"度");
	}


}
