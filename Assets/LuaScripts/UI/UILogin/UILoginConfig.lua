--[[
-- added by wsh @ 2017-11-30
-- UILogin模块窗口配置，要使用还需要导出到UI.Config.UIConfig.lua
-- 一个模块可以对应多个窗口，每个窗口对应一个配置项
-- 使用范例：
-- 窗口配置表 ={
--		名字Name
--		UI层级Layer
-- 		控制器类Controller
--		模型类Model
--		视图类View
--		资源加载路径PrefabPath
-- } 
--]]

-- 窗口配置
local UILogin = {
	Name = UIWindowNames.UILogin,
	Layer = UILayers.BackgroudLayer,
	Model = require "UI.UILogin.Login.UILoginModel",
	Ctrl = require "UI.UILogin.Login.UILoginCtrl",
	View = require "UI.UILogin.Login.UILoginView",
	PrefabPath = "UI/Prefabs/View/UILogin.prefab",
}

local UILoginServer = {
	Name = UIWindowNames.UILoginServer,
	Layer = UILayers.NormalLayer,
	Model = require "UI.UILogin.LoginServer.UILoginServerModel",
	Ctrl = require "UI.UILogin.LoginServer.UILoginServerCtrl",
	View = require "UI.UILogin.LoginServer.UILoginServerView",
	PrefabPath = "UI/Prefabs/View/UILoginServer.prefab",
}

return {
	-- 配置
	UILogin = UILogin,
	UILoginServer = UILoginServer,
}