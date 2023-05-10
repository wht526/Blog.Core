﻿using System.Linq.Expressions;
using System.Reflection;
using Blog.Core.IServices;
using Blog.Core.Model;
using Blog.Core.Model.Models;
using Blog.Core.Model.ViewModels;
using Blog.Core.Repository.UnitOfWorks;
using Blog.Core.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Blog.Core.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(Permissions.Name)]
    public class TasksQzController : ControllerBase
    {
        private readonly ITasksQzServices _tasksQzServices;
        private readonly ITasksLogServices _tasksLogServices;
        private readonly ISchedulerCenter _schedulerCenter;
        private readonly IUnitOfWorkManage _unitOfWorkManage;

        public TasksQzController(ITasksQzServices tasksQzServices, ISchedulerCenter schedulerCenter, IUnitOfWorkManage unitOfWorkManage, ITasksLogServices tasksLogServices)
        {
            _unitOfWorkManage = unitOfWorkManage;
            _tasksQzServices = tasksQzServices;
            _schedulerCenter = schedulerCenter;
            _tasksLogServices = tasksLogServices;
        }

        /// <summary>
        /// 分页获取
        /// </summary>
        /// <param name="page"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        // GET: api/Buttons/5
        [HttpGet]
        public async Task<MessageModel<PageModel<TasksQz>>> Get(int page = 1, string key = "")
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(key))
            {
                key = "";
            }
            int intPageSize = 50;

            Expression<Func<TasksQz, bool>> whereExpression = a => a.IsDeleted != true && (a.Name != null && a.Name.Contains(key));

            var data = await _tasksQzServices.QueryPage(whereExpression, page, intPageSize, " Id desc ");
            if (data.dataCount > 0)
            {
                foreach (var item in data.data)
                {
                    item.Triggers = await _schedulerCenter.GetTaskStaus(item);
                }
            }
            return MessageModel<PageModel<TasksQz>>.Message(data.dataCount >= 0, "获取成功", data);
        }

        /// <summary>
        /// 添加计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<MessageModel<string>> Post([FromBody] TasksQz tasksQz)
        {
            var data = new MessageModel<string>();
            _unitOfWorkManage.BeginTran();
            var id = (await _tasksQzServices.Add(tasksQz));
            data.success = id > 0;
            try
            {
                if (data.success)
                {
                    tasksQz.Id = id;
                    data.response = id.ObjToString();
                    data.msg = "添加成功";
                    if (tasksQz.IsStart)
                    {
                        //如果是启动自动
                        var ResuleModel = await _schedulerCenter.AddScheduleJobAsync(tasksQz);
                        data.success = ResuleModel.success;
                        if (ResuleModel.success)
                        {
                            data.msg = $"{data.msg}=>启动成功=>{ResuleModel.msg}";
                        }
                        else
                        {
                            data.msg = $"{data.msg}=>启动失败=>{ResuleModel.msg}";
                        }
                    }
                }
                else
                {
                    data.msg = "添加失败";

                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (data.success)
                    _unitOfWorkManage.CommitTran();
                else
                    _unitOfWorkManage.RollbackTran();
            }
            return data;
        }


        /// <summary>
        /// 修改计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<MessageModel<string>> Put([FromBody] TasksQz tasksQz)
        {
            var data = new MessageModel<string>();
            if (tasksQz != null && tasksQz.Id > 0)
            {
                _unitOfWorkManage.BeginTran();
                data.success = await _tasksQzServices.Update(tasksQz);
                try
                {
                    if (data.success)
                    {
                        data.msg = "修改成功";
                        data.response = tasksQz?.Id.ObjToString();
                        if (tasksQz.IsStart)
                        {
                            var ResuleModelStop = await _schedulerCenter.StopScheduleJobAsync(tasksQz);
                            data.msg = $"{data.msg}=>停止:{ResuleModelStop.msg}";
                            var ResuleModelStar = await _schedulerCenter.AddScheduleJobAsync(tasksQz);
                            data.success = ResuleModelStar.success;
                            data.msg = $"{data.msg}=>启动:{ResuleModelStar.msg}";
                        }
                        else
                        {
                            var ResuleModelStop = await _schedulerCenter.StopScheduleJobAsync(tasksQz);
                            data.msg = $"{data.msg}=>停止:{ResuleModelStop.msg}";
                        }
                    }
                    else
                    {
                        data.msg = "修改失败";
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (data.success)
                        _unitOfWorkManage.CommitTran();
                    else
                        _unitOfWorkManage.RollbackTran();
                }
            }
            return data;
        }
        /// <summary>
        /// 删除一个任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<MessageModel<string>> Delete(long jobId)
        {
            var data = new MessageModel<string>();

            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {
                _unitOfWorkManage.BeginTran();
                data.success = await _tasksQzServices.Delete(model);
                try
                {
                    data.response = jobId.ObjToString();
                    if (data.success)
                    {
                        data.msg = "删除成功";
                        var ResuleModel = await _schedulerCenter.StopScheduleJobAsync(model);
                        data.msg = $"{data.msg}=>任务状态=>{ResuleModel.msg}";
                    }
                    else
                    {
                        data.msg = "删除失败";
                    }

                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (data.success)
                        _unitOfWorkManage.CommitTran();
                    else
                        _unitOfWorkManage.RollbackTran();
                }
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;

        }
        /// <summary>
        /// 启动计划任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<MessageModel<string>> StartJob(long jobId)
        {
            var data = new MessageModel<string>();

            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {
                _unitOfWorkManage.BeginTran();
                try
                {
                    model.IsStart = true;
                    data.success = await _tasksQzServices.Update(model);
                    data.response = jobId.ObjToString();
                    if (data.success)
                    {
                        data.msg = "更新成功";
                        var ResuleModel = await _schedulerCenter.AddScheduleJobAsync(model);
                        data.success = ResuleModel.success;
                        if (ResuleModel.success)
                        {
                            data.msg = $"{data.msg}=>启动成功=>{ResuleModel.msg}";

                        }
                        else
                        {
                            data.msg = $"{data.msg}=>启动失败=>{ResuleModel.msg}";
                        }
                    }
                    else
                    {
                        data.msg = "更新失败";
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (data.success)
                        _unitOfWorkManage.CommitTran();
                    else
                        _unitOfWorkManage.RollbackTran();
                }
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;
        }
        /// <summary>
        /// 停止一个计划任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>        
        [HttpGet]
        public async Task<MessageModel<string>> StopJob(long jobId)
        {
            var data = new MessageModel<string>();

            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {
                model.IsStart = false;
                data.success = await _tasksQzServices.Update(model);
                data.response = jobId.ObjToString();
                if (data.success)
                {
                    data.msg = "更新成功";
                    var ResuleModel = await _schedulerCenter.StopScheduleJobAsync(model);
                    if (ResuleModel.success)
                    {
                        data.msg = $"{data.msg}=>停止成功=>{ResuleModel.msg}";
                    }
                    else
                    {
                        data.msg = $"{data.msg}=>停止失败=>{ResuleModel.msg}";
                    }
                }
                else
                {
                    data.msg = "更新失败";
                }
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;
        }
        /// <summary>
        /// 暂停一个计划任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>        
        [HttpGet]
        public async Task<MessageModel<string>> PauseJob(long jobId)
        {
            var data = new MessageModel<string>();
            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {
                _unitOfWorkManage.BeginTran();
                try
                {
                    data.success = await _tasksQzServices.Update(model);
                    data.response = jobId.ObjToString();
                    if (data.success)
                    {
                        data.msg = "更新成功";
                        var ResuleModel = await _schedulerCenter.PauseJob(model);
                        if (ResuleModel.success)
                        {
                            data.msg = $"{data.msg}=>暂停成功=>{ResuleModel.msg}";
                        }
                        else
                        {
                            data.msg = $"{data.msg}=>暂停失败=>{ResuleModel.msg}";
                        }
                        data.success = ResuleModel.success;
                    }
                    else
                    {
                        data.msg = "更新失败";
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (data.success)
                        _unitOfWorkManage.CommitTran();
                    else
                        _unitOfWorkManage.RollbackTran();
                }
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;
        }
        /// <summary>
        /// 恢复一个计划任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>        
        [HttpGet]
        public async Task<MessageModel<string>> ResumeJob(long jobId)
        {
            var data = new MessageModel<string>();

            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {
                _unitOfWorkManage.BeginTran();
                try
                {
                    model.IsStart = true;
                    data.success = await _tasksQzServices.Update(model);
                    data.response = jobId.ObjToString();
                    if (data.success)
                    {
                        data.msg = "更新成功";
                        var ResuleModel = await _schedulerCenter.ResumeJob(model);
                        if (ResuleModel.success)
                        {
                            data.msg = $"{data.msg}=>恢复成功=>{ResuleModel.msg}";
                        }
                        else
                        {
                            data.msg = $"{data.msg}=>恢复失败=>{ResuleModel.msg}";
                        }
                        data.success = ResuleModel.success;
                    }
                    else
                    {
                        data.msg = "更新失败";
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (data.success)
                        _unitOfWorkManage.CommitTran();
                    else
                        _unitOfWorkManage.RollbackTran();
                }
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;
        }
        /// <summary>
        /// 重启一个计划任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<MessageModel<string>> ReCovery(long jobId)
        {
            var data = new MessageModel<string>();
            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {

                _unitOfWorkManage.BeginTran();
                try
                {
                    model.IsStart = true;
                    data.success = await _tasksQzServices.Update(model);
                    data.response = jobId.ObjToString();
                    if (data.success)
                    {
                        data.msg = "更新成功";
                        var ResuleModelStop = await _schedulerCenter.StopScheduleJobAsync(model);
                        var ResuleModelStar = await _schedulerCenter.AddScheduleJobAsync(model);
                        if (ResuleModelStar.success)
                        {
                            data.msg = $"{data.msg}=>停止:{ResuleModelStop.msg}=>启动:{ResuleModelStar.msg}";
                            data.response = jobId.ObjToString();

                        }
                        else
                        {
                            data.msg = $"{data.msg}=>停止:{ResuleModelStop.msg}=>启动:{ResuleModelStar.msg}";
                            data.response = jobId.ObjToString();
                        }
                        data.success = ResuleModelStar.success;
                    }
                    else
                    {
                        data.msg = "更新失败";
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (data.success)
                        _unitOfWorkManage.CommitTran();
                    else
                        _unitOfWorkManage.RollbackTran();
                }
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;

        }
        /// <summary>
        /// 获取任务命名空间
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public MessageModel<List<QuartzReflectionViewModel>> GetTaskNameSpace()
        {
            var baseType = typeof(IJob);
            var path = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
            var referencedAssemblies = System.IO.Directory.GetFiles(path, "Blog.Core.Tasks.dll").Select(Assembly.LoadFrom).ToArray();
            var types = referencedAssemblies
                .SelectMany(a => a.DefinedTypes)
                .Select(type => type.AsType())
                .Where(x => x != baseType && baseType.IsAssignableFrom(x)).ToArray();
            var implementTypes = types.Where(x => x.IsClass).Select(item => new QuartzReflectionViewModel { nameSpace = item.Namespace, nameClass = item.Name, remark = "" }).ToList();
            return MessageModel<List<QuartzReflectionViewModel>>.Success("获取成功", implementTypes);
        }

        /// <summary>
        /// 立即执行任务
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<MessageModel<string>> ExecuteJob(long jobId)
        {
            var data = new MessageModel<string>();

            var model = await _tasksQzServices.QueryById(jobId);
            if (model != null)
            {
                return await _schedulerCenter.ExecuteJobAsync(model);
            }
            else
            {
                data.msg = "任务不存在";
            }
            return data;
        }
        /// <summary>
        /// 获取任务运行日志
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<MessageModel<PageModel<TasksLog>>> GetTaskLogs(long jobId, int page = 1, int pageSize = 10, DateTime? runTimeStart = null, DateTime? runTimeEnd = null)
        {
            var model = await _tasksLogServices.GetTaskLogs(jobId, page, pageSize, runTimeStart, runTimeEnd);
            return MessageModel<PageModel<TasksLog>>.Message(model.dataCount >= 0, "获取成功", model);
        }
        /// <summary>
        /// 任务概况
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<MessageModel<object>> GetTaskOverview(long jobId, int page = 1, int pageSize = 10, DateTime? runTimeStart = null, DateTime? runTimeEnd = null, string type = "month")
        {
            var model = await _tasksLogServices.GetTaskOverview(jobId, runTimeStart, runTimeEnd, type);
            return MessageModel<object>.Message(true, "获取成功", model);
        }

    }
}
