--[[
-- added by wsh @ 2017-12-18
-- UILoading模块窗口配置，要使用还需要导出到UI.Config.UIConfig.lua
--]]

-- 窗口配置
local UILoading = {
	Name = UIWindowNames.UILoading,
	Layer = UILayers.TopLayer,
	Model = require "UI.UILoading.Loading.UILoadingModel",
	Ctrl = nil,
	View = require "UI.UILoading.Loading.UILoadingView",
	PrefabPath = "UI/Prefabs/View/UILoading.prefab",
}

return {
	-- 配置
	UILoading = UILoading,
}