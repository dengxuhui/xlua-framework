-- 全局模块
require "Global.Global"
	
-- 定义为全局模块，整个lua程序的入口类
GameMain = {};

-- 全局初始化
local function Initilize()
	local loadingAssetbundlePath = "UI/Prefabs/View/UILoading.prefab"
	ResourcesManager:I():CoLoadAssetBundleAsync(loadingAssetbundlePath)
end

-- 进入游戏
local function EnterGame()
	-- TODO：服务器信息应该从服务器上拉取，这里读取测试数据
	local ServerData = require "DataCenter.ServerData.ServerData"
	local TestServerData = require "GameTest.DataTest.TestServerData"
	local ClientData = require "DataCenter.ClientData.ClientData"
	ServerData:I():ParseServerList(TestServerData)
	local selected = ClientData:I().login_server_id
	if selected == nil or ServerData:I().servers[selected] == nil then
		ClientData:I():SetLoginServerID(10001)
	end
	
	SceneManager:I():SwitchScene(SceneConfig.LoginScene)
	
	--Logger.Log("###################################################")
end

--主入口函数。从这里开始lua逻辑
local function Start()
	print("GameMain start...")
	
	-- 模块启动
	UpdateManager:I():Startup()
	TimerManager:I():Startup()
	LogicUpdater:I():Startup()
	UIManager:I():Startup()
	
	if Config.Debug then
		-- 单元测试
		local UnitTest = require "UnitTest.UnitTestMain"
		UnitTest.Run()
	end
	
	coroutine.start(function()
		Initilize()
		EnterGame()
	end)
end

-- 场景切换通知
local function OnLevelWasLoaded(level)
	collectgarbage("collect")
	Time.timeSinceLevelLoad = 0
end

local function OnApplicationQuit()
	-- 模块注销
	UpdateManager:I():Dispose()
	TimerManager:I():Dispose()
	LogicUpdater:I():Dispose()
end

-- GameMain公共接口，其它的一律为私有接口，只能在本模块访问
GameMain.Start = Start
GameMain.OnLevelWasLoaded = OnLevelWasLoaded
GameMain.OnApplicationQuit = OnApplicationQuit

return GameMain